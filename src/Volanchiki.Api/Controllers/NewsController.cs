using Microsoft.AspNetCore.Mvc;
using Volanchiki.Api.Data;

namespace Volanchiki.Api.Controllers;

[ApiController]
[Route("api/news")]
public class NewsController : ControllerBase
{
    readonly NewsStore _news;
    readonly CompanyStore _companies;

    public NewsController(NewsStore news, CompanyStore companies)
    {
        _news = news;
        _companies = companies;
    }

    /// <summary>
    /// Лента новостей из RSS. Опционально: q — текстовый фильтр, companyId — лёгкое ранжирование под ОК.
    /// </summary>
    [HttpGet]
    public IActionResult List(
        [FromQuery] string? q = null,
        [FromQuery] string? companyId = null,
        [FromQuery] int take = 20)
    {
        string? companyContext = null;
        if (!string.IsNullOrWhiteSpace(companyId))
        {
            var c = _companies.Get(companyId);
            if (c != null)
            {
                companyContext = string.Join(' ',
                    c.OkvedName, c.OkvedCode, c.City, c.Region, c.ClientType);
            }
        }

        var hits = _news.Search(q, take, companyContext);
        return Ok(new
        {
            items = hits.Select(h => new
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
                // текст события для insights/run
                text = BuildEventText(h.Item.Title, h.Item.Summary),
            }),
            meta = new
            {
                total = _news.Count,
                live = _news.LiveCount,
                lastSuccessUtc = _news.LastSuccessUtc,
                lastAttemptUtc = _news.LastAttemptUtc,
                lastError = _news.LastError,
            },
        });
    }

    static string BuildEventText(string title, string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            return title;
        if (summary.StartsWith(title, StringComparison.OrdinalIgnoreCase))
            return summary;
        return title + ". " + summary;
    }
}
