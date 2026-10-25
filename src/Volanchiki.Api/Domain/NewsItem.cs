namespace Volanchiki.Api.Domain;

public class NewsItem
{
    public string Id { get; set; } = "";
    public string Source { get; set; } = "";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string? Url { get; set; }
    public string Category { get; set; } = "Прочее";
    /// <summary>Экономика / бизнес / финансы — для фильтра ленты инсайтов.</summary>
    public bool BusinessRelevant { get; set; }
    /// <summary>Ждёт уточнения рубрики лёгкой LLM (спорные / Прочее).</summary>
    public bool NeedsLlm { get; set; }
    /// <summary>rules | rules_ambiguous | rules_other | llm | seed</summary>
    public string ClassificationSource { get; set; } = "rules";
    /// <summary>Кандидаты от правил при неоднозначности — подсказка для LLM.</summary>
    public List<string> RuleCandidates { get; set; } = new();
    public DateTimeOffset PublishedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset FetchedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsSeed { get; set; }
}
