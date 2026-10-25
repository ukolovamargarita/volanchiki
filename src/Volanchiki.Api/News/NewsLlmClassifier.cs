using System.Text.Json;
using Volanchiki.Api.Data;
using Volanchiki.Api.Domain;
using Volanchiki.Api.Llm;

namespace Volanchiki.Api.News;

/// <summary>Уточняет рубрику для спорных и «Прочее» лёгкой моделью (пачками).</summary>
public class NewsLlmClassifier
{
    readonly LlmClient _llm;
    readonly NewsStore _store;
    readonly ILogger<NewsLlmClassifier> _log;

    public NewsLlmClassifier(LlmClient llm, NewsStore store, ILogger<NewsLlmClassifier> log)
    {
        _llm = llm;
        _store = store;
        _log = log;
    }

    public async Task<int> ClassifyPendingAsync(int maxItems, CancellationToken ct)
    {
        if (!_llm.IsConfigured || maxItems <= 0)
            return 0;

        var batch = _store.ClaimPendingLlm(maxItems);
        if (batch.Count == 0)
            return 0;

        try
        {
            var applied = await ClassifyBatchAsync(batch, ct);
            _log.LogInformation("news-llm: classified {Applied}/{Batch}", applied, batch.Count);
            return applied;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "news-llm: batch failed, requeue {N}", batch.Count);
            _store.RequeueLlm(batch.Select(b => b.Id));
            return 0;
        }
    }

    async Task<int> ClassifyBatchAsync(IReadOnlyList<NewsItem> batch, CancellationToken ct)
    {
        var cats = string.Join(", ", NewsClassifier.AllowedCategories);
        var system =
            "Ты классификатор деловых новостей РФ. Ответь ТОЛЬКО JSON-массивом без markdown. " +
            "Каждый элемент: {\"id\":\"...\",\"category\":\"...\",\"business\":true|false}. " +
            $"category — ровно одно из: {cats}. " +
            "business=true если новость важна для бизнеса/экономики/финансов компании. " +
            "Если кандидаты указаны — выбери среди них или Прочее. Не выдумывай id.";

        var payload = batch.Select(n => new
        {
            id = n.Id,
            title = n.Title,
            summary = n.Summary.Length > 280 ? n.Summary[..280] : n.Summary,
            candidates = n.RuleCandidates,
            provisional = n.Category,
        });

        var user = JsonSerializer.Serialize(payload);
        var raw = await _llm.ChatAsync(
            system,
            user,
            model: _llm.PickModel(LlmRole.Light),
            temperature: 0f,
            ct: ct);

        var applied = 0;
        var answered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var doc = JsonDocument.Parse(StripFence(raw));
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                _store.RequeueLlm(batch.Select(b => b.Id));
                return 0;
            }

            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var id = el.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                var cat = el.TryGetProperty("category", out var cEl) ? cEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(id) || !NewsClassifier.IsAllowed(cat))
                    continue;

                var business = el.TryGetProperty("business", out var bEl)
                    && bEl.ValueKind is JsonValueKind.True or JsonValueKind.False
                    && bEl.GetBoolean();

                // нормализуем регистр категории
                cat = NewsClassifier.AllowedCategories
                    .First(a => a.Equals(cat, StringComparison.OrdinalIgnoreCase));

                if (cat is not "Прочее" and not "Регион")
                    business = true;

                _store.ApplyLlmClassification(id, cat, business);
                answered.Add(id);
                applied++;
            }
        }
        catch (JsonException ex)
        {
            _log.LogWarning(ex, "news-llm: bad json {Raw}", Trunc(raw, 200));
            _store.RequeueLlm(batch.Select(b => b.Id));
            return 0;
        }

        var missing = batch.Select(b => b.Id).Where(id => !answered.Contains(id)).ToList();
        if (missing.Count > 0)
            _store.RequeueLlm(missing);

        return applied;
    }

    static string StripFence(string s)
    {
        s = s.Trim();
        if (!s.StartsWith("```"))
            return s;
        var lines = s.Split('\n');
        return string.Join("\n", lines.Skip(1).TakeWhile(l => !l.StartsWith("```"))).Trim();
    }

    static string Trunc(string s, int n) =>
        s.Length <= n ? s : s[..n] + "…";
}
