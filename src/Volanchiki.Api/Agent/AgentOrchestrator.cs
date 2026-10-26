using System.Text.Json;
using Volanchiki.Api.Domain;
using Volanchiki.Api.Llm;

namespace Volanchiki.Api.Agent;

public class AgentOrchestrator
{
    const int MaxSteps = 10;
    static readonly HashSet<string> AllowedTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "get_company_profile", "search_news", "search_alfa_analytics", "draft_insight",
    };

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
        => await RunCoreAsync(companyId, newsHint, clarify: null, priorInsight: null, ct);

    public async Task<AgentRunResult> ClarifyAsync(
        string companyId, string? newsHint, string question, InsightCard prior, CancellationToken ct)
        => await RunCoreAsync(companyId, newsHint, question, prior, ct);

    async Task<AgentRunResult> RunCoreAsync(
        string companyId,
        string? newsHint,
        string? clarify,
        InsightCard? priorInsight,
        CancellationToken ct)
    {
        var session = new AgentSession
        {
            CompanyId = companyId,
            NewsHint = newsHint,
        };
        if (!string.IsNullOrWhiteSpace(clarify))
            session.Scratch["clarify"] = clarify;
        if (priorInsight != null)
            session.Scratch["prior_insight"] = JsonSerializer.Serialize(priorInsight);

        session.Note("start", detail: "company=" + companyId + (clarify != null ? "; clarify" : ""));

        var llmPlanFails = 0;
        var lastTool = "";
        var sameToolStreak = 0;

        try
        {
            for (var i = 0; i < MaxSteps; i++)
            {
                ct.ThrowIfCancellationRequested();

                AgentDecision decision;
                if (llmPlanFails >= 2 || !_llm.IsConfigured)
                {
                    decision = LocalPlan(session);
                    if (llmPlanFails >= 2)
                        session.Note("plan_fallback", detail: "local after llm fails");
                }
                else
                {
                    decision = await LlmPlanAsync(session, ct);
                    if (decision.Action == "invalid")
                    {
                        llmPlanFails++;
                        session.Note("plan_invalid", detail: decision.Reason);
                        decision = LocalPlan(session);
                    }
                }

                session.Note(decision.Action, decision.Tool, decision.Reason);

                if (decision.Action == "finish")
                    break;

                if (decision.Action != "tool" || string.IsNullOrWhiteSpace(decision.Tool))
                    break;

                if (!AllowedTools.Contains(decision.Tool))
                {
                    session.Note("tool_denied", decision.Tool);
                    llmPlanFails++;
                    continue;
                }

                // антизацикливание: тот же tool подряд без broaden → local
                if (string.Equals(decision.Tool, lastTool, StringComparison.OrdinalIgnoreCase))
                {
                    sameToolStreak++;
                    if (sameToolStreak >= 2 && decision.Tool != "search_news" && decision.Tool != "search_alfa_analytics")
                    {
                        session.Note("plan_loop", decision.Tool, "switch to local");
                        llmPlanFails = 2;
                        continue;
                    }
                }
                else
                {
                    sameToolStreak = 0;
                    lastTool = decision.Tool;
                }

                if (decision.Tool == "draft_insight" && !_llm.IsConfigured)
                {
                    session.Note("skip_draft", detail: "no llm key");
                    break;
                }

                // уже есть результат — не зовём снова (кроме broaden-поиска)
                if (decision.Tool != "draft_insight"
                    && session.Scratch.ContainsKey(ScratchKey(decision.Tool))
                    && !IsBroaden(decision))
                {
                    session.Note("tool_skip", decision.Tool, "already have");
                    continue;
                }

                if (!_tools.TryGetValue(decision.Tool, out var tool))
                {
                    session.Note("tool_missing", decision.Tool);
                    break;
                }

                var argsJson = PrepareArgs(session, decision.Tool!, decision.ArgsJson, clarify);
                using var argsDoc = JsonDocument.Parse(argsJson);
                _log.LogInformation("agent {Run} -> {Tool}", session.RunId, tool.Name);

                var result = await tool.ExecuteAsync(argsDoc.RootElement, session, ct);
                session.Scratch["last_" + tool.Name] = result;
                session.Scratch[ScratchKey(tool.Name)] = result;
                if (tool.Name == "draft_insight")
                    session.Scratch["draft"] = result;
                session.Note("tool_result", tool.Name, TraceSummary.ForTool(tool.Name, result));

                // адаптация: пустая выдача → расширенный повторный поиск
                if (tool.Name == "search_news" && !InsightSourceBuilder.HasNewsHits(session)
                    && !session.Scratch.ContainsKey("news_broadened"))
                {
                    session.Scratch["news_broadened"] = "1";
                    session.Scratch.Remove("news");
                    session.Note("adapt_broaden_news", detail: "empty hits");
                    if (_tools.TryGetValue("search_news", out var newsTool))
                    {
                        using var broad = JsonDocument.Parse("{\"query\":\"\",\"take\":8}");
                        var again = await newsTool.ExecuteAsync(broad.RootElement, session, ct);
                        session.Scratch["news"] = again;
                        session.Scratch["last_search_news"] = again;
                        session.Note("tool_result", "search_news", TraceSummary.ForTool("search_news", again));
                    }
                }

                if (tool.Name == "search_alfa_analytics" && !InsightSourceBuilder.HasAlfaHits(session)
                    && !session.Scratch.ContainsKey("alfa_broadened"))
                {
                    session.Scratch["alfa_broadened"] = "1";
                    session.Scratch.Remove("alfa");
                    session.Note("adapt_broaden_alfa", detail: "empty hits");
                    if (_tools.TryGetValue("search_alfa_analytics", out var alfaTool))
                    {
                        var q = ShortTokens(session.NewsHint);
                        using var broad = JsonDocument.Parse(
                            "{\"query\":" + JsonSerializer.Serialize(q) + ",\"take\":5}");
                        var again = await alfaTool.ExecuteAsync(broad.RootElement, session, ct);
                        session.Scratch["alfa"] = again;
                        session.Scratch["last_search_alfa_analytics"] = again;
                        session.Note("tool_result", "search_alfa_analytics",
                            TraceSummary.ForTool("search_alfa_analytics", again));
                    }
                }
            }

            await EnsurePipelineAsync(session, clarify, ct);

            var insight = BuildInsight(session);
            var check = await SelfCheckAsync(session, insight, ct);
            if (!check.Ok && _tools.TryGetValue("draft_insight", out var draftTool))
            {
                session.Note("retry_draft", detail: check.FixHint);
                using var fixArgs = JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    topic = session.NewsHint,
                    fix_hint = check.FixHint,
                    clarify,
                }));
                await draftTool.ExecuteAsync(fixArgs.RootElement, session, ct);
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

    async Task EnsurePipelineAsync(AgentSession session, string? clarify, CancellationToken ct)
    {
        if (session.Scratch.ContainsKey("draft") || !_llm.IsConfigured)
            return;

        async Task RunTool(string name, string args)
        {
            if (!_tools.TryGetValue(name, out var tool)) return;
            session.Note("ensure_" + name.Replace("search_", "").Replace("get_", ""));
            using var doc = JsonDocument.Parse(args);
            var result = await tool.ExecuteAsync(doc.RootElement, session, ct);
            session.Scratch[ScratchKey(name)] = result;
            session.Scratch["last_" + name] = result;
            if (name == "draft_insight") session.Scratch["draft"] = result;
            session.Note("tool_result", name, TraceSummary.ForTool(name, result));
        }

        if (!session.Scratch.ContainsKey("profile"))
            await RunTool("get_company_profile", "{\"company_id\":" + JsonSerializer.Serialize(session.CompanyId) + "}");
        if (!session.Scratch.ContainsKey("news"))
            await RunTool("search_news", "{\"query\":" + JsonSerializer.Serialize(session.NewsHint ?? "") + "}");
        if (!session.Scratch.ContainsKey("alfa"))
            await RunTool("search_alfa_analytics", "{\"query\":" + JsonSerializer.Serialize(session.NewsHint ?? "") + "}");
        if (!session.Scratch.ContainsKey("draft"))
        {
            await RunTool("draft_insight", JsonSerializer.Serialize(new
            {
                topic = session.NewsHint,
                clarify,
            }));
        }
    }

    async Task<AgentDecision> LlmPlanAsync(AgentSession session, CancellationToken ct)
    {
        try
        {
            var have = string.Join(", ", session.Scratch.Keys.OrderBy(k => k));
            var system =
                "Ты планировщик агента. Ответь ТОЛЬКО JSON: " +
                "{\"action\":\"tool\"|\"finish\",\"tool\":string|null,\"args\":object,\"reason\":string}. " +
                "Разрешённые tool: get_company_profile, search_news, search_alfa_analytics, draft_insight. " +
                "Не вызывай tool, результат которого уже есть в have. " +
                "Сначала профиль, потом новости и/или аналитика, затем draft_insight. " +
                "finish — только когда draft уже есть.";

            var user =
                $"company={session.CompanyId}\n" +
                $"event={session.NewsHint}\n" +
                $"have=[{have}]\n" +
                $"news_hits={InsightSourceBuilder.HasNewsHits(session)}\n" +
                $"alfa_hits={InsightSourceBuilder.HasAlfaHits(session)}";

            var raw = await _llm.ChatAsync(system, user, _llm.PickModel(LlmRole.Light), 0f, ct);
            var d = ParseDecision(raw);
            if (d.Action == "tool" && (d.Tool == null || !AllowedTools.Contains(d.Tool)))
                return new AgentDecision("invalid", d.Tool, null, "bad tool");
            return d;
        }
        catch (Exception ex)
        {
            return new AgentDecision("invalid", null, null, ex.Message);
        }
    }

    static AgentDecision LocalPlan(AgentSession session)
    {
        if (!session.Scratch.ContainsKey("profile"))
        {
            return new AgentDecision(
                "tool",
                "get_company_profile",
                "{\"company_id\":" + JsonSerializer.Serialize(session.CompanyId) + "}",
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
            return new AgentDecision(
                "tool",
                "draft_insight",
                JsonSerializer.Serialize(new
                {
                    topic = session.NewsHint,
                    clarify = session.Scratch.GetValueOrDefault("clarify"),
                }),
                "need draft");
        }

        return new AgentDecision("finish", null, null, "pipeline done");
    }

    async Task<(bool Ok, string? FixHint)> SelfCheckAsync(
        AgentSession session, InsightCard insight, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(insight.WhyMattersForCompany))
            return (false, "no company link — свяжи вывод с ОКВЭД/продуктами/регионом из профиля");

        if (!InsightSourceBuilder.HasAlfaHits(session)
            && !string.IsNullOrWhiteSpace(insight.BankView)
            && !insight.BankView.Contains("опоры", StringComparison.OrdinalIgnoreCase)
            && !insight.BankView.Contains("нет", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "нет hits аналитики банка — bank_view должен явно сказать, что опоры нет");
        }

        if (!_llm.IsConfigured)
            return (true, null);

        try
        {
            var ctx = new
            {
                insight,
                has_news = InsightSourceBuilder.HasNewsHits(session),
                has_alfa = InsightSourceBuilder.HasAlfaHits(session),
                profile_present = session.Scratch.ContainsKey("profile"),
                sources_count = insight.Sources.Count,
            };

            var raw = await _llm.ChatAsync(
                "Проверь инсайт. Учти has_news/has_alfa/profile_present. " +
                "JSON: {\"ok\":true|false,\"fix_hint\":string|null}. " +
                "ok=false если нет связи с компанией, выдуманные факты или bank_view при has_alfa=false без признания отсутствия опоры.",
                JsonSerializer.Serialize(ctx),
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
                var bank = InsightSourceBuilder.SanitizeBankView(Str(e, "bank_view"), session);
                return new InsightCard
                {
                    CompanyId = session.CompanyId,
                    Title = Str(e, "title") ?? "Инсайт",
                    WhatHappened = Str(e, "what_happened") ?? "",
                    WhyMattersForCompany = Str(e, "why_matters_for_company") ?? "",
                    BankView = bank,
                    ModelView = Str(e, "model_view") ?? "",
                    Risks = StrArr(e, "risks"),
                    Opportunities = StrArr(e, "opportunities"),
                    SuggestedActions = StrArr(e, "suggested_actions"),
                    Relevance = Str(e, "relevance") ?? "medium",
                    Sources = InsightSourceBuilder.FromSession(session, e),
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
            BankView = InsightSourceBuilder.SanitizeBankView(null, session),
            ModelView = "Нет draft.",
            SuggestedActions = new List<string> { "Связаться с менеджером" },
            Relevance = "low",
            Sources = InsightSourceBuilder.FromSession(session),
        };
    }

    static string ScratchKey(string tool) => tool switch
    {
        "get_company_profile" => "profile",
        "search_news" => "news",
        "search_alfa_analytics" => "alfa",
        "draft_insight" => "draft",
        _ => tool,
    };

    static bool IsBroaden(AgentDecision d) =>
        d.ArgsJson != null && d.ArgsJson.Contains("\"query\":\"\"", StringComparison.Ordinal);

    static string PrepareArgs(AgentSession session, string tool, string? argsJson, string? clarify)
    {
        if (!string.Equals(tool, "draft_insight", StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson;

        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);
            string? topic = session.NewsHint;
            string? fix = null;
            string? cl = clarify ?? session.Scratch.GetValueOrDefault("clarify");

            if (doc.RootElement.TryGetProperty("topic", out var t) && t.ValueKind == JsonValueKind.String)
                topic = t.GetString() ?? topic;
            if (doc.RootElement.TryGetProperty("fix_hint", out var f) && f.ValueKind == JsonValueKind.String)
                fix = f.GetString();
            if (doc.RootElement.TryGetProperty("clarify", out var cq) && cq.ValueKind == JsonValueKind.String)
                cl = cq.GetString() ?? cl;

            return JsonSerializer.Serialize(new { topic, fix_hint = fix, clarify = cl });
        }
        catch
        {
            return JsonSerializer.Serialize(new { topic = session.NewsHint, clarify });
        }
    }

    static string ShortTokens(string? q)
    {
        if (string.IsNullOrWhiteSpace(q)) return "ставка инфляция макро";
        var parts = q.Split(new[] { ' ', ',', '.', ':', ';', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Length >= 4)
            .Take(4);
        var s = string.Join(' ', parts);
        return string.IsNullOrWhiteSpace(s) ? q[..Math.Min(40, q.Length)] : s;
    }

    static AgentDecision ParseDecision(string raw)
    {
        using var doc = JsonDocument.Parse(StripFence(raw));
        var e = doc.RootElement;
        var action = e.GetProperty("action").GetString() ?? "finish";
        string? tool = null;
        if (e.TryGetProperty("tool", out var t) && t.ValueKind == JsonValueKind.String)
            tool = t.GetString();
        string? argsJson = e.TryGetProperty("args", out var a) ? a.GetRawText() : "{}";
        var reason = e.TryGetProperty("reason", out var r) ? r.GetString() : null;
        return new AgentDecision(action, tool, argsJson, reason);
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
