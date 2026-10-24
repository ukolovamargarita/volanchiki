using System.Text.Json;
using Volanchiki.Api.Domain;
using Volanchiki.Api.Llm;

namespace Volanchiki.Api.Agent;

public class AgentOrchestrator
{
    const int MaxSteps = 8;

    readonly LlmClient _llm;
    readonly Dictionary<string, IAgentTool> _tools;
    readonly ILogger<AgentOrchestrator> _log;

    public AgentOrchestrator(LlmClient llm, IEnumerable<IAgentTool> tools, ILogger<AgentOrchestrator> log)
    {
        _llm = llm;
        _tools = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        _log = log;
    }

    public async Task<AgentRunResult> RunAsync(string companyId, string? newsHint, CancellationToken ct)
    {
        var session = new AgentSession
        {
            CompanyId = companyId,
            NewsHint = newsHint,
        };
        session.Note("start", detail: "company=" + companyId);

        try
        {
            for (var i = 0; i < MaxSteps; i++)
            {
                ct.ThrowIfCancellationRequested();

                var decision = await DecideNextAsync(session, ct);
                session.Note(decision.Action, decision.Tool, decision.Reason);

                if (decision.Action == "finish")
                    break;

                if (decision.Action != "tool" || string.IsNullOrWhiteSpace(decision.Tool))
                    break;

                if (decision.Tool == "draft_insight" && !_llm.IsConfigured)
                {
                    session.Note("skip_draft", detail: "no llm key");
                    break;
                }

                if (!_tools.TryGetValue(decision.Tool, out var tool))
                {
                    session.Note("tool_missing", decision.Tool);
                    break;
                }

                using var argsDoc = JsonDocument.Parse(decision.ArgsJson ?? "{}");
                _log.LogInformation("agent {Run} -> {Tool}", session.RunId, tool.Name);

                var result = await tool.ExecuteAsync(argsDoc.RootElement, session, ct);
                session.Scratch["last_" + tool.Name] = result;
                if (tool.Name == "draft_insight")
                    session.Scratch["draft"] = result;
                session.Note("tool_result", tool.Name, result);
            }

            // без draft — дожимаем сами (LLM-планировщик часто его пропускает)
            if (!session.Scratch.ContainsKey("draft") && _llm.IsConfigured
                && _tools.TryGetValue("draft_insight", out var ensureDraft))
            {
                if (!session.Scratch.ContainsKey("news")
                    && _tools.TryGetValue("search_news", out var ensureNews))
                {
                    session.Note("ensure_news");
                    using var newsArgs = JsonDocument.Parse(
                        "{\"query\":" + JsonSerializer.Serialize(session.NewsHint ?? "") + "}");
                    await ensureNews.ExecuteAsync(newsArgs.RootElement, session, ct);
                }

                if (!session.Scratch.ContainsKey("alfa")
                    && _tools.TryGetValue("search_alfa_analytics", out var ensureAlfa))
                {
                    session.Note("ensure_alfa");
                    using var alfaArgs = JsonDocument.Parse(
                        "{\"query\":" + JsonSerializer.Serialize(session.NewsHint ?? "") + "}");
                    await ensureAlfa.ExecuteAsync(alfaArgs.RootElement, session, ct);
                }

                session.Note("ensure_draft");
                using var draftArgs = JsonDocument.Parse(
                    "{\"topic\":" + JsonSerializer.Serialize(session.NewsHint) + "}");
                await ensureDraft.ExecuteAsync(draftArgs.RootElement, session, ct);
            }

            var insight = BuildInsight(session);
            var check = await SelfCheckAsync(session, insight, ct);
            if (!check.Ok && _tools.TryGetValue("draft_insight", out var draft))
            {
                session.Note("retry_draft", detail: check.FixHint);
                using var fixArgs = JsonDocument.Parse(
                    "{\"topic\":" + JsonSerializer.Serialize(session.NewsHint) + "}");
                await draft.ExecuteAsync(fixArgs.RootElement, session, ct);
                insight = BuildInsight(session);
            }

            session.Note("done");
            return new AgentRunResult
            {
                RunId = session.RunId,
                Insight = insight,
                Trace = session.Trace,
                Status = "ok",
            };
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "agent failed {Run}", session.RunId);
            session.Note("error", detail: ex.Message);
            return new AgentRunResult
            {
                RunId = session.RunId,
                Trace = session.Trace,
                Status = "error",
                Error = ex.Message,
            };
        }
    }

    Task<AgentDecision> DecideNextAsync(AgentSession session, CancellationToken ct)
    {
        // без LLM-планировщика: light-модель зацикливалась на profile
        // агентность = tools + порядок + self-check
        return Task.FromResult(LocalPlan(session));
    }

    static AgentDecision LocalPlan(AgentSession session)
    {
        if (!session.Scratch.ContainsKey("profile"))
        {
            return new AgentDecision(
                "tool",
                "get_company_profile",
                "{\"company_id\":\"" + session.CompanyId + "\"}",
                "need profile");
        }

        if (!session.Scratch.ContainsKey("news"))
        {
            return new AgentDecision(
                "tool",
                "search_news",
                "{\"query\":" + JsonSerializer.Serialize(session.NewsHint ?? "") + "}",
                "need news");
        }

        if (!session.Scratch.ContainsKey("alfa"))
        {
            return new AgentDecision(
                "tool",
                "search_alfa_analytics",
                "{\"query\":" + JsonSerializer.Serialize(session.NewsHint ?? "") + "}",
                "need alfa");
        }

        if (!session.Scratch.ContainsKey("draft"))
        {
            // без ключа draft не зовём — BuildInsight отдаст шаблон
            // (IsConfigured проверим снаружи через ensure_draft)
            return new AgentDecision(
                "tool",
                "draft_insight",
                "{\"topic\":" + JsonSerializer.Serialize(session.NewsHint) + "}",
                "need draft");
        }

        return new AgentDecision("finish", null, null, "pipeline done");
    }

    async Task<(bool Ok, string? FixHint)> SelfCheckAsync(
        AgentSession session, InsightCard insight, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(insight.WhyMattersForCompany))
            return (false, "no company link");

        if (!_llm.IsConfigured)
            return (true, null);

        try
        {
            var raw = await _llm.ChatAsync(
                "Check insight. JSON: {\"ok\":true|false,\"fix_hint\":string|null}",
                JsonSerializer.Serialize(insight),
                _llm.PickModel(LlmRole.Light),
                0f,
                ct);

            using var doc = JsonDocument.Parse(StripFence(raw));
            var ok = doc.RootElement.GetProperty("ok").GetBoolean();
            string? hint = null;
            if (doc.RootElement.TryGetProperty("fix_hint", out var h) && h.ValueKind == JsonValueKind.String)
                hint = h.GetString();

            session.Note("self_check", detail: ok ? "ok" : hint);
            return (ok, hint);
        }
        catch
        {
            return (true, null);
        }
    }

    InsightCard BuildInsight(AgentSession session)
    {
        string? draft = null;
        if (session.Scratch.TryGetValue("draft", out draft)
            || session.Scratch.TryGetValue("last_draft_insight", out draft))
        {
            try
            {
                using var doc = JsonDocument.Parse(StripFence(draft));
                var e = doc.RootElement;
                return new InsightCard
                {
                    CompanyId = session.CompanyId,
                    Title = Str(e, "title") ?? "Инсайт",
                    WhatHappened = Str(e, "what_happened") ?? "",
                    WhyMattersForCompany = Str(e, "why_matters_for_company") ?? "",
                    BankView = Str(e, "bank_view") ?? "",
                    ModelView = Str(e, "model_view") ?? "",
                    Risks = StrArr(e, "risks"),
                    Opportunities = StrArr(e, "opportunities"),
                    SuggestedActions = StrArr(e, "suggested_actions"),
                    Relevance = Str(e, "relevance") ?? "medium",
                    Sources = ParseSources(e),
                };
            }
            catch (Exception ex)
            {
                session.Note("draft_parse_fail", detail: ex.Message);
            }
        }

        return new InsightCard
        {
            CompanyId = session.CompanyId,
            Title = "Черновик (без LLM)",
            WhatHappened = session.NewsHint ?? "newsHint не задан",
            WhyMattersForCompany = session.Scratch.ContainsKey("profile")
                ? "См. профиль компании (продукты, оквэд, группа)."
                : "Мало данных.",
            BankView = session.Scratch.ContainsKey("alfa")
                ? "Аналитика банка загружена, но draft не собран."
                : "Поиск по аналитике банка не выполнялся.",
            ModelView = "Нет draft.",
            SuggestedActions = new List<string> { "Связаться с менеджером" },
            Relevance = "low",
            Sources = new List<InsightSource>
            {
                new InsightSource
                {
                    Kind = "company_feature",
                    Title = "profile",
                    Excerpt = session.Scratch.GetValueOrDefault("profile"),
                },
            },
        };
    }

    static AgentDecision ParseDecision(string raw)
    {
        using var doc = JsonDocument.Parse(StripFence(raw));
        var e = doc.RootElement;
        var action = e.GetProperty("action").GetString() ?? "finish";
        string? tool = null;
        if (e.TryGetProperty("tool", out var t) && t.ValueKind == JsonValueKind.String)
            tool = t.GetString();
        string? argsJson = e.TryGetProperty("args", out var a) ? a.GetRawText() : null;
        var reason = e.TryGetProperty("reason", out var r) ? r.GetString() : null;
        return new AgentDecision(action, tool, argsJson, reason);
    }

    static List<InsightSource> ParseSources(JsonElement e)
    {
        var list = new List<InsightSource>();
        if (!e.TryGetProperty("sources", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var s in arr.EnumerateArray())
        {
            list.Add(new InsightSource
            {
                Kind = Str(s, "kind") ?? "news",
                Title = Str(s, "title") ?? "",
                Excerpt = Str(s, "excerpt"),
                Url = Str(s, "url"),
            });
        }

        return list;
    }

    static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    static List<string> StrArr(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return new List<string>();

        var list = new List<string>();
        foreach (var x in arr.EnumerateArray())
        {
            if (x.ValueKind == JsonValueKind.String)
            {
                var s = x.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    list.Add(s);
            }
        }

        return list;
    }

    static string StripFence(string s)
    {
        s = s.Trim();
        if (!s.StartsWith("```"))
            return s;

        var lines = s.Split('\n');
        return string.Join("\n", lines.Skip(1).TakeWhile(l => !l.StartsWith("```"))).Trim();
    }
}

public record AgentDecision(string Action, string? Tool, string? ArgsJson, string? Reason);
