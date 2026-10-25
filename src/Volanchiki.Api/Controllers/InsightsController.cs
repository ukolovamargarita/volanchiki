using Microsoft.AspNetCore.Mvc;
using Volanchiki.Api.Agent;
using Volanchiki.Api.Data;

namespace Volanchiki.Api.Controllers;

[ApiController]
[Route("api/insights")]
public class InsightsController : ControllerBase
{
    readonly IServiceScopeFactory _scopes;
    readonly CompanyStore _companies;
    readonly InsightRunStore _runs;
    readonly ILogger<InsightsController> _log;

    public InsightsController(
        IServiceScopeFactory scopes,
        CompanyStore companies,
        InsightRunStore runs,
        ILogger<InsightsController> log)
    {
        _scopes = scopes;
        _companies = companies;
        _runs = runs;
        _log = log;
    }

    public class RunRequest
    {
        public string CompanyId { get; set; } = "";
        public string? NewsHint { get; set; }
    }

    /// <summary>
    /// Стартует прогон и сразу возвращает runId. Результат — через GET /api/insights/runs/{runId}.
    /// Так обрыв сети на клиенте не убивает уже идущий расчёт на сервере.
    /// </summary>
    [HttpPost("run")]
    public IActionResult Start([FromBody] RunRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.CompanyId))
            return BadRequest(new { error = "companyId_required" });

        if (_companies.Get(req.CompanyId) == null)
            return NotFound(new { error = "company_not_found" });

        var job = _runs.Start(req.CompanyId, req.NewsHint);
        _ = Task.Run(() => ExecuteJobAsync(job.RunId, req.CompanyId, req.NewsHint));

        return Accepted(new { runId = job.RunId, status = "running" });
    }

    [HttpGet("runs/{runId}")]
    public IActionResult GetRun(string runId)
    {
        var job = _runs.Get(runId);
        if (job == null)
            return NotFound(new { error = "run_not_found", runId });

        if (job.Status == "running")
        {
            return Ok(new
            {
                runId = job.RunId,
                status = "running",
                startedAt = job.StartedAt,
                trace = Array.Empty<object>(),
            });
        }

        if (job.Result != null)
            return Ok(job.Result);

        return Ok(new
        {
            runId = job.RunId,
            status = job.Status,
            error = "empty_result",
            trace = Array.Empty<object>(),
        });
    }

    async Task ExecuteJobAsync(string runId, string companyId, string? newsHint)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var agent = scope.ServiceProvider.GetRequiredService<AgentOrchestrator>();
            var result = await agent.RunAsync(companyId, newsHint, CancellationToken.None);
            _runs.Complete(runId, result);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "insight job failed {RunId}", runId);
            _runs.Fail(runId, ex.Message);
        }
    }
}
