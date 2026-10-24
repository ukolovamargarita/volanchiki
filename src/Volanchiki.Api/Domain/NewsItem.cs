namespace Volanchiki.Api.Domain;

public class NewsItem
{
    public string Id { get; set; } = "";
    public string Source { get; set; } = "";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string? Url { get; set; }
    public string Category { get; set; } = "Прочее";
    public DateTimeOffset PublishedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset FetchedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsSeed { get; set; }
}
