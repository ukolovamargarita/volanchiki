namespace Volanchiki.Api.Domain;

public class Company
{
    public string Id { get; set; } = "";
    public string? OrgType { get; set; }
    public string? OkfsName { get; set; }
    public string? AgeBand { get; set; }
    public string? EcoMark { get; set; }
    public string? OkvedCode { get; set; }
    public string? OkvedName { get; set; }
    public bool HasKeyOwnerInfo { get; set; }
    public string? WorkersFns { get; set; }
    public string? WorkersSpark { get; set; }
    public string ClientType { get; set; } = "";
    public bool HasRko { get; set; }
    public bool HasGuarantee { get; set; }
    public bool HasLoan { get; set; }
    public bool HasLeasing { get; set; }
    public bool HasDeposit { get; set; }
    public bool HasPayroll { get; set; }
    public bool HasFactoring { get; set; }
    public bool FtsFlag { get; set; }
    public string? FtsCountry { get; set; }
    public decimal? Revenue { get; set; }
    public string? GroupId { get; set; }
    public string? City { get; set; }
    public string? Region { get; set; }

    public bool IsActiveClient =>
        string.Equals(ClientType, "CLIENT", StringComparison.OrdinalIgnoreCase);
}

public class PersonLink
{
    public string PersonId { get; set; } = "";
    public string? City { get; set; }
    public string? Region { get; set; }
    public string CompanyId { get; set; } = "";
}

public class PaymentEdge
{
    public string BeneficiaryId { get; set; } = "";
    public string PayerId { get; set; } = "";
    public decimal Amount { get; set; }
    public int Count { get; set; }
}
