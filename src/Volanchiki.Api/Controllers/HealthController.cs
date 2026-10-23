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
    readonly LlmClient _llm;

    public HealthController(CompanyStore store, AlfaAnalyticsStore alfa, LlmClient llm)
    {
        _store = store;
        _alfa = alfa;
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
            llmConfigured = _llm.IsConfigured,
            utc = DateTimeOffset.UtcNow,
        });
    }
}
