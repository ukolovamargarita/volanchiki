using Microsoft.AspNetCore.Mvc;
using Volanchiki.Api.Data;

namespace Volanchiki.Api.Controllers;

[ApiController]
[Route("api/metrics")]
public class MetricsController : ControllerBase
{
    readonly MetricsDb _db;

    public MetricsController(MetricsDb db) => _db = db;

    /// <summary>Сводка метрик качества и нагрузки (из SQLite).</summary>
    [HttpGet]
    public IActionResult Get() => Ok(_db.Snapshot());
}
