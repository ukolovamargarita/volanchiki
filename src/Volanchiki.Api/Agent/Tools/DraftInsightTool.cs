using System.Text.Json;
using Volanchiki.Api.Llm;

namespace Volanchiki.Api.Agent.Tools;

public class DraftInsightTool : IAgentTool
{
    private readonly LlmClient _llm;

    public DraftInsightTool(LlmClient llm) => _llm = llm;

    public string Name => "draft_insight";
    public string Description => "Собрать JSON-инсайт по профилю компании и новости.";

    public async Task<string> ExecuteAsync(JsonElement args, AgentSession session, CancellationToken ct)
    {
        session.Scratch.TryGetValue("profile", out var profile);
        session.Scratch.TryGetValue("alfa", out var alfa);

        var topic = session.NewsHint;
        if (args.TryGetProperty("topic", out var t))
            topic = t.GetString() ?? topic;
        if (string.IsNullOrWhiteSpace(topic))
            topic = "ключевая ставка ЦБ";

        var system =
            "Ответь только JSON без markdown. Поля: title, what_happened, why_matters_for_company, " +
            "bank_view, model_view, risks[], opportunities[], suggested_actions[], " +
            "relevance (low|medium|high), sources[{kind,title,excerpt}]. " +
            "why_matters_for_company — по признакам компании. " +
            "bank_view — только из блока аналитики банка (цитируй файл/фрагмент), иначе скажи что опоры нет. " +
            "model_view — вывод модели, не позиция банка. Цифры не выдумывай.";

        var user =
            $"company={session.CompanyId}\n" +
            $"event={topic}\n\n" +
            $"profile:\n{profile ?? "(empty)"}\n\n" +
            $"alfa_analytics:\n{alfa ?? "(empty)"}";

        var raw = await _llm.ChatAsync(
            system,
            user,
            model: _llm.PickModel(LlmRole.Reasoning),
            temperature: 0.3f,
            ct: ct);

        var cleaned = StripFence(raw);
        session.Scratch["draft"] = cleaned;
        return cleaned;
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
