using Microsoft.Extensions.Options;
using Volanchiki.Api.Data;
using Volanchiki.Api.Options;

namespace Volanchiki.Api.News;

/// <summary>Фоновый опрос RSS (по умолчанию раз в 3 минуты) — блок «Парсер» кейса.</summary>
public class RssNewsIngestionHostedService : BackgroundService
{
    readonly IHttpClientFactory _httpFactory;
    readonly NewsStore _store;
    readonly RssNewsParser _parser;
    readonly IOptionsMonitor<NewsOptions> _options;
    readonly ILogger<RssNewsIngestionHostedService> _log;

    public RssNewsIngestionHostedService(
        IHttpClientFactory httpFactory,
        NewsStore store,
        RssNewsParser parser,
        IOptionsMonitor<NewsOptions> options,
        ILogger<RssNewsIngestionHostedService> log)
    {
        _httpFactory = httpFactory;
        _store = store;
        _parser = parser;
        _options = options;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _store.EnsureSeed(RssNewsParser.BuildSeedFallback());
        _log.LogInformation("news: seed ready, live={Live}", _store.LiveCount);

        // первый проход сразу
        await PollOnceAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var sec = Math.Clamp(_options.CurrentValue.PollIntervalSeconds, 60, 600);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(sec), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await PollOnceAsync(stoppingToken);
        }
    }

    async Task PollOnceAsync(CancellationToken ct)
    {
        var feeds = _options.CurrentValue.Feeds;
        if (feeds.Count == 0)
        {
            _store.MarkAttempt("no_feeds_configured");
            return;
        }

        var client = _httpFactory.CreateClient("rss");
        var errors = new List<string>();
        var totalNew = 0;

        foreach (var feed in feeds)
        {
            if (string.IsNullOrWhiteSpace(feed.Url))
                continue;

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, feed.Url);
                using var res = await client.SendAsync(req, ct);
                if (!res.IsSuccessStatusCode)
                {
                    errors.Add($"{feed.Name}:{ (int)res.StatusCode}");
                    continue;
                }

                var xml = await res.Content.ReadAsStringAsync(ct);
                var items = _parser.Parse(xml, string.IsNullOrWhiteSpace(feed.Name) ? feed.Url : feed.Name);
                var added = _store.UpsertMany(items);
                totalNew += added;
                _log.LogInformation("news: {Source} parsed={Count} new={New}", feed.Name, items.Count, added);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add($"{feed.Name}:{ex.GetType().Name}");
                _log.LogWarning(ex, "news: feed failed {Source}", feed.Name);
            }
        }

        if (errors.Count == feeds.Count)
            _store.MarkAttempt(string.Join("; ", errors));
        else
            _store.MarkSuccess();

        if (totalNew > 0)
            _log.LogInformation("news: upserted {New}, total={Total}", totalNew, _store.Count);
    }
}
