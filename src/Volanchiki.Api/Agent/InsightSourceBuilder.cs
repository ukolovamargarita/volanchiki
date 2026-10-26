using System.Text.Json;
using Volanchiki.Api.Domain;

namespace Volanchiki.Api.Agent;

/// <summary>Источники инсайта только из результатов tools — не из фантазии модели.</summary>
public static class InsightSourceBuilder
{
    public static List<InsightSource> FromSession(AgentSession session, JsonElement? draft = null)
    {
        var list = new List<InsightSource>();
        var usedIds = ReadUsedIds(draft);

        if (!string.IsNullOrWhiteSpace(session.NewsHint))
        {
            list.Add(new InsightSource
            {
                Kind = "news",
                Title = "Событие (запрос пользователя)",
                Excerpt = Clip(session.NewsHint!, 240),
            });
        }

        if (session.Scratch.TryGetValue("news", out var newsJson))
        {
            var news = FromNews(newsJson, usedIds).ToList();
            if (news.Count == 0 && usedIds.Count > 0)
                news = FromNews(newsJson, new HashSet<string>()).ToList();
            list.AddRange(news.Take(5));
        }

        if (session.Scratch.TryGetValue("alfa", out var alfaJson))
        {
            var alfa = FromAlfa(alfaJson, usedIds).ToList();
            if (alfa.Count == 0 && usedIds.Count > 0)
                alfa = FromAlfa(alfaJson, new HashSet<string>()).ToList();
            list.AddRange(alfa.Take(5));
        }

        if (session.Scratch.TryGetValue("profile", out var profileJson))
        {
            list.Add(new InsightSource
            {
                Kind = "company_feature",
                Title = "Профиль компании " + session.CompanyId,
                Excerpt = Clip(SummarizeProfile(profileJson), 280),
            });
        }

        return Dedup(list);
    }

    public static bool HasAlfaHits(AgentSession session) => HitCount(session, "alfa") > 0;
    public static bool HasNewsHits(AgentSession session) => HitCount(session, "news") > 0;

    public static string SanitizeBankView(string? bankView, AgentSession session)
    {
        if (!HasAlfaHits(session))
            return "Опоры в аналитике банка по этому событию нет.";

        var text = bankView?.Trim() ?? "";
        return string.IsNullOrWhiteSpace(text)
            ? "Опоры в аналитике банка по этому событию нет."
            : text;
    }

    static int HitCount(AgentSession session, string key)
    {
        if (!session.Scratch.TryGetValue(key, out var json))
            return 0;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("hits", out var hits)
                   && hits.ValueKind == JsonValueKind.Array
                ? hits.GetArrayLength()
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    static HashSet<string> ReadUsedIds(JsonElement? draft)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (draft == null) return set;
        var e = draft.Value;
        if (!e.TryGetProperty("source_ids", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return set;

        foreach (var x in arr.EnumerateArray())
        {
            if (x.ValueKind == JsonValueKind.String)
            {
                var s = x.GetString();
                if (!string.IsNullOrWhiteSpace(s)) set.Add(s);
            }
            else if (x.ValueKind == JsonValueKind.Number)
                set.Add(x.ToString());
        }

        return set;
    }

    static IEnumerable<InsightSource> FromNews(string json, HashSet<string> usedIds)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("hits", out var hits) || hits.ValueKind != JsonValueKind.Array)
            yield break;

        var i = 0;
        foreach (var h in hits.EnumerateArray())
        {
            i++;
            var id = Str(h, "id") ?? ("n" + i);
            if (usedIds.Count > 0 && !MatchesId(usedIds, id, "news", i))
                continue;

            yield return new InsightSource
            {
                Kind = "news",
                Title = Str(h, "title") ?? "Новость",
                Url = Str(h, "url"),
                Excerpt = Clip(Str(h, "summary"), 220),
            };
        }
    }

    static IEnumerable<InsightSource> FromAlfa(string json, HashSet<string> usedIds)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("hits", out var hits) || hits.ValueKind != JsonValueKind.Array)
            yield break;

        var i = 0;
        foreach (var h in hits.EnumerateArray())
        {
            i++;
            var file = Str(h, "file") ?? Str(h, "title") ?? ("alfa-" + i);
            if (usedIds.Count > 0 && !MatchesId(usedIds, file, "alfa", i))
                continue;

            yield return new InsightSource
            {
                Kind = "alfa_analytics",
                Title = file,
                Excerpt = Clip(Str(h, "excerpt"), 220),
            };
        }
    }

    static bool MatchesId(HashSet<string> used, string id, string prefix, int index) =>
        used.Contains(id)
        || used.Contains(prefix + ":" + id)
        || used.Contains(index.ToString())
        || used.Any(u => id.Contains(u, StringComparison.OrdinalIgnoreCase));

    static string SummarizeProfile(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var c = doc.RootElement.TryGetProperty("company", out var co) ? co : doc.RootElement;
            var okved = Str(c, "okvedName") ?? Str(c, "OkvedName") ?? "";
            var city = Str(c, "city") ?? Str(c, "City") ?? "";
            return $"{okved}. {city}".Trim('.', ' ');
        }
        catch
        {
            return Clip(json, 200);
        }
    }

    static List<InsightSource> Dedup(List<InsightSource> list)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var res = new List<InsightSource>();
        foreach (var s in list)
        {
            var key = s.Kind + "|" + s.Title + "|" + (s.Url ?? "");
            if (!seen.Add(key)) continue;
            res.Add(s);
        }

        return res;
    }

    static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    static string Clip(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Trim();
        return s.Length <= max ? s : s[..max].Trim() + "…";
    }
}
