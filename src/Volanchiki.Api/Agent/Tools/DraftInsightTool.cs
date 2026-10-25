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
        session.Scratch.TryGetValue("news", out var news);

        var topic = session.NewsHint;
        if (args.TryGetProperty("topic", out var t))
            topic = t.GetString() ?? topic;
        if (string.IsNullOrWhiteSpace(topic))
            topic = "ключевая ставка ЦБ";

        var system =
            "Ответь только JSON (без markdown-fence). Поля: title, what_happened, why_matters_for_company, " +
            "bank_view, model_view, risks[], opportunities[], suggested_actions[], " +
            "relevance (low|medium|high), sources[{kind,title,excerpt,url}]. " +
            "Формат текста в what_happened / why_matters_for_company / bank_view / model_view: " +
            "2–4 коротких абзаца, разделённых \\n\\n; ключевые цифры, ставки, даты, названия " +
            "выделяй **двойными звёздочками** (пример: ставка **14%**). Не пиши сплошной простынёй. " +
            "В risks / opportunities / suggested_actions — короткие пункты, тоже можно **акценты**. " +
            "what_happened — факты из события и блока news (kind=news, укажи url если есть). " +
            "why_matters_for_company — по признакам компании. " +
            "bank_view — только из блока аналитики банка (цитируй файл/фрагмент, kind=alfa_analytics), иначе скажи что опоры нет. " +
            "model_view — вывод модели, не позиция банка. Цифры не выдумывай.";

        var user =
            $"company={session.CompanyId}\n" +
            $"event={topic}\n\n" +
            $"profile:\n{profile ?? "(empty)"}\n\n" +
            $"news:\n{news ?? "(empty)"}\n\n" +
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
