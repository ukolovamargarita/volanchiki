using Microsoft.AspNetCore.Mvc;
using Volanchiki.Api.Agent;
using Volanchiki.Api.Data;
using Volanchiki.Api.Domain;

namespace Volanchiki.Api.Controllers;

[ApiController]
[Route("api/insights")]
public class InsightsController : ControllerBase
{
    readonly IServiceScopeFactory _scopes;
    readonly CompanyStore _companies;
    readonly InsightRunStore _runs;
    readonly InsightRunGuard _guard;
    readonly ILogger<InsightsController> _log;

    public InsightsController(
        IServiceScopeFactory scopes,
        CompanyStore companies,
        InsightRunStore runs,
        InsightRunGuard guard,
        ILogger<InsightsController> log)
    {
        _scopes = scopes;
        _companies = companies;
        _runs = runs;
        _guard = guard;
        _log = log;
    }

    public class RunRequest
    {
        public string CompanyId { get; set; } = "";
        public string? NewsHint { get; set; }
    }

    public class ClarifyRequest
    {
        public string Question { get; set; } = "";
    }

    [HttpPost("run")]
    public IActionResult Start([FromBody] RunRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.CompanyId))
            return BadRequest(new { error = "companyId_required" });

        if (_companies.Get(req.CompanyId) == null)
            return NotFound(new { error = "company_not_found" });

        var hint = req.NewsHint?.Trim() ?? "";
        if (hint.Length > InsightRunGuard.MaxNewsHintChars)
            return BadRequest(new { error = "newsHint_too_long", max = InsightRunGuard.MaxNewsHintChars });

        var clientKey = ClientKey();
        if (!_guard.TryEnter(clientKey, out var err))
            return StatusCode(429, new { error = err });

        var job = _runs.Start(req.CompanyId, hint);
        _ = Task.Run(() => ExecuteJobAsync(job.RunId, req.CompanyId, hint, acquired: true));

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

    /// <summary>Уточняющий вопрос к уже готовому инсайту — новый прогон с тем же событием + вопрос.</summary>
    [HttpPost("runs/{runId}/clarify")]
    public IActionResult Clarify(string runId, [FromBody] ClarifyRequest req)
    {
        var prev = _runs.Get(runId);
        if (prev == null)
            return NotFound(new { error = "run_not_found", runId });
        if (prev.Status != "ok" || prev.Result?.Insight == null)
            return BadRequest(new { error = "run_not_ready" });

        var q = req.Question?.Trim() ?? "";
        if (q.Length < 3)
            return BadRequest(new { error = "question_required" });
        if (q.Length > 1000)
            return BadRequest(new { error = "question_too_long", max = 1000 });

        if (!_guard.TryEnter(ClientKey(), out var err))
            return StatusCode(429, new { error = err });

        var job = _runs.Start(prev.CompanyId, prev.NewsHint);
        var insight = prev.Result.Insight;
        _ = Task.Run(() => ExecuteClarifyAsync(job.RunId, prev.CompanyId, prev.NewsHint, q, insight));

        return Accepted(new { runId = job.RunId, status = "running", parentRunId = runId });
    }

    async Task ExecuteJobAsync(string runId, string companyId, string? newsHint, bool acquired)
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
        finally
        {
            if (acquired) _guard.Exit();
        }
    }

    async Task ExecuteClarifyAsync(
        string runId, string companyId, string? newsHint, string question, InsightCard prior)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var agent = scope.ServiceProvider.GetRequiredService<AgentOrchestrator>();
            var result = await agent.ClarifyAsync(companyId, newsHint, question, prior, CancellationToken.None);
            _runs.Complete(runId, result);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "clarify job failed {RunId}", runId);
            _runs.Fail(runId, ex.Message);
        }
        finally
        {
            _guard.Exit();
        }
    }

    string ClientKey()
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return ip;
    }
}
