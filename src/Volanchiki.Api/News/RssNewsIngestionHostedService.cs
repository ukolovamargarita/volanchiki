using Microsoft.Extensions.Options;
using Volanchiki.Api.Data;
using Volanchiki.Api.Options;

namespace Volanchiki.Api.News;

/// <summary>Фоновый опрос RSS + LLM-уточнение рубрик для спорных/Прочее.</summary>
public class RssNewsIngestionHostedService : BackgroundService
{
    readonly IHttpClientFactory _httpFactory;
    readonly NewsStore _store;
    readonly RssNewsParser _parser;
    readonly NewsLlmClassifier _llmClassifier;
    readonly IOptionsMonitor<NewsOptions> _options;
    readonly ILogger<RssNewsIngestionHostedService> _log;

    public RssNewsIngestionHostedService(
        IHttpClientFactory httpFactory,
        NewsStore store,
        RssNewsParser parser,
        NewsLlmClassifier llmClassifier,
        IOptionsMonitor<NewsOptions> options,
        ILogger<RssNewsIngestionHostedService> log)
    {
        _httpFactory = httpFactory;
        _store = store;
        _parser = parser;
        _llmClassifier = llmClassifier;
        _options = options;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _store.EnsureSeed(RssNewsParser.BuildSeedFallback());
        _log.LogInformation("news: seed ready, live={Live}", _store.LiveCount);

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
                    errors.Add($"{feed.Name}:{(int)res.StatusCode}");
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
            _log.LogInformation("news: upserted {New}, total={Total}, pendingLlm={Pending}",
                totalNew, _store.Count, _store.PendingLlmCount);

        // пайплайн: правила уже проставили рубрику; LLM — только спорные / Прочее
        var llmCap = Math.Clamp(_options.CurrentValue.LlmClassifyPerPoll, 0, 80);
        if (llmCap > 0 && _store.PendingLlmCount > 0)
        {
            var n = await _llmClassifier.ClassifyPendingAsync(llmCap, ct);
            if (n > 0)
                _log.LogInformation("news: llm rubrics applied={N}, stillPending={P}", n, _store.PendingLlmCount);
        }
    }
}
