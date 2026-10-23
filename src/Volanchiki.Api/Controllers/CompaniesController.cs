using Microsoft.AspNetCore.Mvc;
using Volanchiki.Api.Data;
using Volanchiki.Api.Domain;

namespace Volanchiki.Api.Controllers;

[ApiController]
[Route("api/companies")]
public class CompaniesController : ControllerBase
{
    private readonly CompanyStore _store;

    public CompaniesController(CompanyStore store) => _store = store;

    [HttpGet]
    public IActionResult Search([FromQuery] string? q, [FromQuery] int take = 20)
    {
        if (!_store.IsLoaded)
            return StatusCode(503, new { error = "data_not_loaded" });

        return Ok(_store.SearchClients(q, take).Select(ToDto));
    }

    [HttpGet("demo")]
    public IActionResult Demo([FromQuery] int take = 8)
    {
        if (!_store.IsLoaded)
            return StatusCode(503, new { error = "data_not_loaded" });

        return Ok(_store.PickDemoCandidates(take).Select(ToDto));
    }

    [HttpGet("{id}")]
    public IActionResult Get(string id)
    {
        var c = _store.Get(id);
        if (c == null)
            return NotFound();

        var group = _store.GetGroup(c.GroupId);
        return Ok(new
        {
            company = ToDto(c),
            group = new
            {
                id = c.GroupId,
                size = group.Count,
                revenue = _store.GroupRevenue(c.GroupId),
                members = group.Take(20).Select(ToDto),
            },
            payments = _store.PaymentsFor(c.Id, 15).Select(p => new
            {
                p.PayerId,
                p.BeneficiaryId,
                p.Amount,
                p.Count,
            }),
        });
    }

    static object ToDto(Company c) => new
    {
        c.Id,
        c.ClientType,
        c.OkvedCode,
        c.OkvedName,
        c.Revenue,
        c.City,
        c.Region,
        c.GroupId,
        products = new
        {
            rko = c.HasRko,
            loan = c.HasLoan,
            guarantee = c.HasGuarantee,
            leasing = c.HasLeasing,
            deposit = c.HasDeposit,
            payroll = c.HasPayroll,
            factoring = c.HasFactoring,
        },
    };
}
