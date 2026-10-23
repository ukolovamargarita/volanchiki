using Microsoft.AspNetCore.Mvc;
using Volanchiki.Api.Agent;
using Volanchiki.Api.Data;

namespace Volanchiki.Api.Controllers;

[ApiController]
[Route("api/insights")]
public class InsightsController : ControllerBase
{
    private readonly AgentOrchestrator _agent;
    private readonly CompanyStore _store;

    public InsightsController(AgentOrchestrator agent, CompanyStore store)
    {
        _agent = agent;
        _store = store;
    }

    public class RunRequest
    {
        public string CompanyId { get; set; } = "";
        public string? NewsHint { get; set; }
    }

    [HttpPost("run")]
    public async Task<IActionResult> Run([FromBody] RunRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.CompanyId))
            return BadRequest(new { error = "companyId_required" });

        if (_store.Get(req.CompanyId) == null)
            return NotFound(new { error = "company_not_found" });

        var result = await _agent.RunAsync(req.CompanyId, req.NewsHint, ct);
        if (result.Status != "ok")
            return StatusCode(500, result);

        return Ok(result);
    }
}
