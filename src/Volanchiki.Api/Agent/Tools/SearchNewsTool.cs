using System.Text.Json;
using Volanchiki.Api.Data;

namespace Volanchiki.Api.Agent.Tools;

public class SearchNewsTool : IAgentTool
{
    readonly NewsStore _news;
    readonly CompanyStore _companies;

    public SearchNewsTool(NewsStore news, CompanyStore companies)
    {
        _news = news;
        _companies = companies;
    }

    public string Name => "search_news";
    public string Description =>
        "Поиск по ленте внешних новостей (RSS). args: query (опц.), take (опц.).";

    public Task<string> ExecuteAsync(JsonElement args, AgentSession session, CancellationToken ct)
    {
        var query = session.NewsHint ?? "";
        if (args.TryGetProperty("query", out var q) && q.ValueKind == JsonValueKind.String)
            query = q.GetString() ?? query;

        var take = 5;
        if (args.TryGetProperty("take", out var t) && t.TryGetInt32(out var n))
            take = n;

        string? companyContext = null;
        var company = _companies.Get(session.CompanyId);
        if (company != null)
        {
            companyContext = string.Join(' ',
                company.OkvedName, company.OkvedCode, company.City, company.Region);
        }

        var hits = _news.Search(query, take, companyContext);
        var payload = new
        {
            query,
            count = _news.Count,
            live = _news.LiveCount,
            hits = hits.Select(h => new
            {
                id = h.Item.Id,
                source = h.Item.Source,
                title = h.Item.Title,
                summary = h.Item.Summary,
                url = h.Item.Url,
                category = h.Item.Category,
                publishedAt = h.Item.PublishedAt,
                score = h.Score,
                isSeed = h.Item.IsSeed,
            }),
        };

        var json = JsonSerializer.Serialize(payload);
        session.Scratch["news"] = json;
        return Task.FromResult(json);
    }
}
