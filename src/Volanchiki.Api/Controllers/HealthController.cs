using Microsoft.AspNetCore.Mvc;
using Volanchiki.Api.Data;
using Volanchiki.Api.Llm;

namespace Volanchiki.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    readonly CompanyStore _store;
    readonly AlfaAnalyticsStore _alfa;
    readonly NewsStore _news;
    readonly LlmClient _llm;

    public HealthController(CompanyStore store, AlfaAnalyticsStore alfa, NewsStore news, LlmClient llm)
    {
        _store = store;
        _alfa = alfa;
        _news = news;
        _llm = llm;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "ok",
            dataLoaded = _store.IsLoaded,
            companies = _store.CompanyCount,
            payments = _store.PaymentCount,
            alfaLoaded = _alfa.IsLoaded,
            alfaDocs = _alfa.DocCount,
            newsCount = _news.Count,
            newsLive = _news.LiveCount,
            newsLastSuccessUtc = _news.LastSuccessUtc,
            llmConfigured = _llm.IsConfigured,
            utc = DateTimeOffset.UtcNow,
        });
    }
}
