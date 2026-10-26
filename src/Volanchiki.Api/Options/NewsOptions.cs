namespace Volanchiki.Api.Options;

public class NewsOptions
{
    public const string Section = "News";

    /// <summary>Интервал опроса RSS, секунды (1–5 минут ок для кейса).</summary>
    public int PollIntervalSeconds { get; set; } = 180;

    /// <summary>Сколько новостей классифицировать LLM за один опрос (0 = выкл). Все новые идут в очередь.</summary>
    public int LlmClassifyPerPoll { get; set; } = 40;

    public List<NewsFeedOptions> Feeds { get; set; } = new();
}

public class NewsFeedOptions
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
}
