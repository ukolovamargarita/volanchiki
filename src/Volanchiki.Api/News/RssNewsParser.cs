using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Volanchiki.Api.Domain;

namespace Volanchiki.Api.News;

public class RssNewsParser
{
    static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    static readonly XNamespace Content = "http://purl.org/rss/1.0/modules/content/";
    static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";

    public IReadOnlyList<NewsItem> Parse(string xml, string sourceName)
    {
        var doc = XDocument.Parse(xml, LoadOptions.None);
        if (doc.Root == null)
            return Array.Empty<NewsItem>();

        // RSS 2.0
        var items = doc.Descendants("item").ToList();
        if (items.Count > 0)
            return items.Select(el => FromRssItem(el, sourceName)).Where(n => n != null).Cast<NewsItem>().ToList();

        // Atom
        var entries = doc.Descendants(Atom + "entry").ToList();
        if (entries.Count > 0)
            return entries.Select(el => FromAtomEntry(el, sourceName)).Where(n => n != null).Cast<NewsItem>().ToList();

        return Array.Empty<NewsItem>();
    }

    static NewsItem? FromRssItem(XElement el, string source)
    {
        var title = Clean(el.Element("title")?.Value);
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var link = Clean(el.Element("link")?.Value)
                   ?? Clean(el.Element("guid")?.Value);
        var guid = Clean(el.Element("guid")?.Value) ?? link ?? title;
        var summary = FirstNonEmpty(
            el.Element("description")?.Value,
            el.Element(Content + "encoded")?.Value,
            el.Element("summary")?.Value);

        var published = ParseDate(
            el.Element("pubDate")?.Value
            ?? el.Element(Dc + "date")?.Value);

        summary = StripHtml(summary);
        return BuildItem(source, guid!, title, summary, LooksLikeUrl(link) ? link : null, published);
    }

    static NewsItem? FromAtomEntry(XElement el, string source)
    {
        var title = Clean(el.Element(Atom + "title")?.Value);
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var link = el.Elements(Atom + "link")
            .Select(l => l.Attribute("href")?.Value)
            .FirstOrDefault(h => !string.IsNullOrWhiteSpace(h))
            ?? el.Element(Atom + "id")?.Value;

        var idRaw = Clean(el.Element(Atom + "id")?.Value) ?? link ?? title;
        var summary = FirstNonEmpty(
            el.Element(Atom + "summary")?.Value,
            el.Element(Atom + "content")?.Value);

        var published = ParseDate(
            el.Element(Atom + "published")?.Value
            ?? el.Element(Atom + "updated")?.Value);

        summary = StripHtml(summary);
        return BuildItem(source, idRaw!, title, summary, LooksLikeUrl(link) ? link : null, published);
    }

    static NewsItem BuildItem(
        string source, string rawId, string title, string summary, string? url, DateTimeOffset published)
    {
        var ev = NewsClassifier.Evaluate(title, summary);
        return new NewsItem
        {
            Id = StableId(source, rawId),
            Source = source,
            Title = title,
            Summary = Clip(summary, 600),
            Url = url,
            Category = ev.Category,
            BusinessRelevant = ev.BusinessRelevant,
            NeedsLlm = ev.NeedsLlm,
            ClassificationSource = ev.Source,
            RuleCandidates = ev.MatchedTags.ToList(),
            PublishedAt = published,
            IsSeed = false,
        };
    }

    public static IReadOnlyList<NewsItem> BuildSeedFallback()
    {
        var now = DateTimeOffset.UtcNow;
        return new[]
        {
            Seed("seed-rate", "Демо", "Ставка",
                "ЦБ сохранил ключевую ставку 14%",
                "Банк России сохранил ключевую ставку на уровне 14% годовых, риторика стала жёстче.",
                now.AddHours(-2)),
            Seed("seed-inflation", "Демо", "Инфляция",
                "Недельная инфляция: свежие данные Росстата",
                "Росстат опубликовал данные по недельной инфляции — ориентир для денежно-кредитной политики.",
                now.AddHours(-5)),
            Seed("seed-gold", "Демо", "Металлы",
                "Рынок золота: ценовая динамика",
                "Обзор рынка золота: движение цен и спроса на драгоценный металл.",
                now.AddHours(-8)),
            Seed("seed-labor", "Демо", "Макро",
                "Рынок труда не показывает охлаждения",
                "Макростатистика: данные по рынку труда не подтверждают охлаждения экономики.",
                now.AddHours(-12)),
            Seed("seed-gdp", "Демо", "Макро",
                "Слабый рост ВВП в первом полугодии",
                "Макростатистика: слабый рост ВВП, прогноз роста пересмотрен.",
                now.AddHours(-18)),
        };
    }

    static NewsItem Seed(string id, string source, string cat, string title, string summary, DateTimeOffset at) =>
        new()
        {
            Id = id,
            Source = source,
            Category = cat,
            BusinessRelevant = true,
            NeedsLlm = false,
            ClassificationSource = "seed",
            Title = title,
            Summary = summary,
            PublishedAt = at,
            FetchedAt = DateTimeOffset.UtcNow,
            IsSeed = true,
        };

    static string StableId(string source, string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(source + "|" + raw.Trim()));
        return Convert.ToHexString(bytes.AsSpan(0, 12)).ToLowerInvariant();
    }

    static DateTimeOffset ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return DateTimeOffset.UtcNow;
        if (DateTimeOffset.TryParse(raw, out var dto))
            return dto.ToUniversalTime();
        return DateTimeOffset.UtcNow;
    }

    static string StripHtml(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return "";
        var sb = new StringBuilder(s.Length);
        var inTag = false;
        foreach (var ch in s)
        {
            if (ch == '<') { inTag = true; continue; }
            if (ch == '>') { inTag = false; continue; }
            if (!inTag) sb.Append(ch);
        }

        return System.Net.WebUtility.HtmlDecode(sb.ToString())
            .Replace('\u00a0', ' ')
            .Trim();
    }

    static string Clean(string? s) =>
        string.IsNullOrWhiteSpace(s) ? "" : System.Net.WebUtility.HtmlDecode(s.Trim());

    static string FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v;
        }

        return "";
    }

    static string Clip(string s, int max)
    {
        s = s.Replace("\r\n", "\n").Trim();
        if (s.Length <= max) return s;
        return s.Substring(0, max).Trim() + "...";
    }

    static bool LooksLikeUrl(string? s) =>
        !string.IsNullOrWhiteSpace(s)
        && (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
}
