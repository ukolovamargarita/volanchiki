using System.Text.Json;
using Volanchiki.Api.Data;

namespace Volanchiki.Api.Agent.Tools;

public class SearchAlfaAnalyticsTool : IAgentTool
{
    readonly AlfaAnalyticsStore _store;

    public SearchAlfaAnalyticsTool(AlfaAnalyticsStore store) => _store = store;

    public string Name => "search_alfa_analytics";
    public string Description =>
        "Поиск по аналитике Альфа-Банка (txt). args: query, take (опц.).";

    public Task<string> ExecuteAsync(JsonElement args, AgentSession session, CancellationToken ct)
    {
        var query = session.NewsHint ?? "";
        if (args.TryGetProperty("query", out var q) && q.ValueKind == JsonValueKind.String)
            query = q.GetString() ?? query;

        var take = 3;
        if (args.TryGetProperty("take", out var t) && t.TryGetInt32(out var n))
            take = n;

        if (!_store.IsLoaded)
        {
            var empty = "{\"hits\":[],\"warning\":\"alfa_not_loaded\"}";
            session.Scratch["alfa"] = empty;
            return Task.FromResult(empty);
        }

        var hits = _store.Search(query, take);
        var payload = new
        {
            query,
            hits = hits.Select(h => new
            {
                file = h.FileName,
                title = h.Title,
                excerpt = h.Excerpt,
                score = h.Score,
            }),
        };

        var json = JsonSerializer.Serialize(payload);
        session.Scratch["alfa"] = json;
        return Task.FromResult(json);
    }
}
