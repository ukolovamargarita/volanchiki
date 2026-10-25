using System.Text.Json;
using System.Text.Encodings.Web;

namespace Volanchiki.Api.Agent;

/// <summary>Короткий человекочитаемый текст для трассировки (вместо сырого JSON с \uXXXX).</summary>
public static class TraceSummary
{
    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    public static string ForTool(string tool, string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(StripFence(raw));
            var e = doc.RootElement;
            return tool switch
            {
                "get_company_profile" => SummarizeProfile(e),
                "search_news" => SummarizeNews(e),
                "search_alfa_analytics" => SummarizeAlfa(e),
                "draft_insight" => SummarizeDraft(e),
                _ => Clip(raw, 500),
            };
        }
        catch
        {
            return Clip(raw, 500);
        }
    }

    static string SummarizeProfile(JsonElement e)
    {
        var c = e.TryGetProperty("company", out var co) ? co : e;
        var id = Str(c, "id") ?? Str(c, "Id") ?? "?";
        var okved = Str(c, "okvedName") ?? Str(c, "OkvedName") ?? "—";
        var city = Str(c, "city") ?? Str(c, "City") ?? "";
        var rev = c.TryGetProperty("revenue", out var r) || c.TryGetProperty("Revenue", out r)
            ? r.ToString()
            : "—";
        var groupSize = e.TryGetProperty("group", out var g) && g.TryGetProperty("size", out var sz)
            ? sz.ToString()
            : "?";
        return $"Компания {id}: {okved}. {city}. Выручка {rev}. В группе {groupSize}.";
    }

    static string SummarizeNews(JsonElement e)
    {
        var q = Str(e, "query") ?? "";
        var hits = e.TryGetProperty("hits", out var arr) && arr.ValueKind == JsonValueKind.Array
            ? arr.EnumerateArray().ToList()
            : new List<JsonElement>();
        var lines = new List<string>
        {
            $"Найдено в ленте: {hits.Count} (запрос: «{Clip(q, 80)}»)",
        };
        foreach (var h in hits.Take(4))
        {
            var title = Str(h, "title") ?? "—";
            var cat = Str(h, "category") ?? "";
            var src = Str(h, "source") ?? "";
            lines.Add($"• [{cat}] {title} — {src}");
        }

        return string.Join("\n", lines);
    }

    static string SummarizeAlfa(JsonElement e)
    {
        var hits = e.TryGetProperty("hits", out var arr) && arr.ValueKind == JsonValueKind.Array
            ? arr.EnumerateArray().ToList()
            : new List<JsonElement>();
        if (hits.Count == 0)
            return "По аналитике банка совпадений нет.";

        var lines = new List<string> { $"Фрагментов аналитики: {hits.Count}" };
        foreach (var h in hits.Take(3))
        {
            var file = Str(h, "file") ?? Str(h, "title") ?? "файл";
            var excerpt = Str(h, "excerpt") ?? "";
            lines.Add($"• {file}: {Clip(excerpt, 120)}");
        }

        return string.Join("\n", lines);
    }

    static string SummarizeDraft(JsonElement e)
    {
        var title = Str(e, "title") ?? "без заголовка";
        var what = Str(e, "what_happened") ?? "";
        var rel = Str(e, "relevance") ?? "";
        return $"Черновик «{title}» (значимость: {rel}).\n{Clip(what.Replace("\\n", " "), 220)}";
    }

    static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    static string Clip(string s, int max)
    {
        s = s.Replace("\r\n", "\n").Trim();
        if (s.Length <= max) return s;
        return s.Substring(0, max).Trim() + "…";
    }

    static string StripFence(string s)
    {
        s = s.Trim();
        if (!s.StartsWith("```")) return s;
        var lines = s.Split('\n');
        return string.Join("\n", lines.Skip(1).TakeWhile(l => !l.StartsWith("```"))).Trim();
    }
}
