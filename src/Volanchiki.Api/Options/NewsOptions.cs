namespace Volanchiki.Api.Options;

public class NewsOptions
{
    public const string Section = "News";

    /// <summary>Интервал опроса RSS, секунды (1–5 минут ок для кейса).</summary>
    public int PollIntervalSeconds { get; set; } = 180;

    public List<NewsFeedOptions> Feeds { get; set; } = new();
}

public class NewsFeedOptions
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
}
