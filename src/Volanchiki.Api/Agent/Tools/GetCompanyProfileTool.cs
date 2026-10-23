using System.Text.Json;
using Volanchiki.Api.Data;

namespace Volanchiki.Api.Agent.Tools;

public class GetCompanyProfileTool : IAgentTool
{
    private readonly CompanyStore _store;

    public GetCompanyProfileTool(CompanyStore store) => _store = store;

    public string Name => "get_company_profile";
    public string Description => "Профиль компании: оквэд, продукты, группа, платежи.";

    public Task<string> ExecuteAsync(JsonElement args, AgentSession session, CancellationToken ct)
    {
        var id = session.CompanyId;
        if (args.TryGetProperty("company_id", out var p))
            id = p.GetString() ?? id;

        var company = _store.Get(id);
        if (company == null)
            return Task.FromResult("{\"error\":\"company_not_found\",\"id\":\"" + id + "\"}");

        var group = _store.GetGroup(company.GroupId);
        var payload = new
        {
            company = new
            {
                company.Id,
                company.ClientType,
                company.OkvedCode,
                company.OkvedName,
                company.Revenue,
                company.City,
                company.Region,
                company.GroupId,
                products = new
                {
                    company.HasRko,
                    company.HasLoan,
                    company.HasGuarantee,
                    company.HasLeasing,
                    company.HasDeposit,
                    company.HasPayroll,
                    company.HasFactoring,
                },
                company.FtsFlag,
                company.FtsCountry,
            },
            group = new
            {
                size = group.Count,
                revenue = _store.GroupRevenue(company.GroupId),
                members = group.Take(12).Select(m => new { m.Id, m.OkvedName, m.Revenue }),
            },
            top_payments = _store.PaymentsFor(company.Id, 8).Select(x => new
            {
                x.PayerId,
                x.BeneficiaryId,
                x.Amount,
                x.Count,
            }),
        };

        var json = JsonSerializer.Serialize(payload);
        session.Scratch["profile"] = json;
        return Task.FromResult(json);
    }
}
