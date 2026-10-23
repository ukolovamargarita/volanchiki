using Volanchiki.Api.Domain;

namespace Volanchiki.Api.Data;

public class CompanyStore
{
    readonly Dictionary<string, Company> _byId = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, List<Company>> _byGroup = new(StringComparer.OrdinalIgnoreCase);
    readonly List<PersonLink> _people = new();
    readonly List<PaymentEdge> _payments = new();
    readonly object _gate = new();

    public bool IsLoaded { get; private set; }
    public int CompanyCount => _byId.Count;
    public int PaymentCount => _payments.Count;

    public void ReplaceAll(
        IEnumerable<Company> companies,
        IEnumerable<PersonLink> people,
        IEnumerable<PaymentEdge> payments)
    {
        lock (_gate)
        {
            _byId.Clear();
            _byGroup.Clear();
            _people.Clear();
            _payments.Clear();

            foreach (var c in companies)
            {
                if (string.IsNullOrWhiteSpace(c.Id))
                    continue;

                _byId[c.Id] = c;

                if (!string.IsNullOrWhiteSpace(c.GroupId))
                {
                    if (!_byGroup.TryGetValue(c.GroupId, out var list))
                    {
                        list = new List<Company>();
                        _byGroup[c.GroupId] = list;
                    }

                    list.Add(c);
                }
            }

            _people.AddRange(people);
            _payments.AddRange(payments);
            IsLoaded = true;
        }
    }

    public Company? Get(string id)
    {
        lock (_gate)
            return _byId.TryGetValue(id, out var c) ? c : null;
    }

    public IReadOnlyList<Company> GetGroup(string? groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
            return Array.Empty<Company>();

        lock (_gate)
            return _byGroup.TryGetValue(groupId, out var list) ? list.ToList() : [];
    }

    public decimal GroupRevenue(string? groupId)
    {
        var members = GetGroup(groupId);
        decimal sum = 0;
        foreach (var m in members)
        {
            if (m.Revenue == null || m.Revenue <= 1000)
                continue; // в csv 1000 = мусор
            sum += m.Revenue.Value;
        }

        return sum;
    }

    public IReadOnlyList<Company> SearchClients(string? query, int take = 20)
    {
        take = Math.Clamp(take, 1, 100);
        lock (_gate)
        {
            IEnumerable<Company> q = _byId.Values.Where(c => c.IsActiveClient);

            if (!string.IsNullOrWhiteSpace(query))
            {
                q = q.Where(c =>
                    c.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || (c.OkvedName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (c.City?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (c.GroupId?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            return q.Take(take).ToList();
        }
    }

    public IReadOnlyList<PaymentEdge> PaymentsFor(string companyId, int take = 30)
    {
        lock (_gate)
        {
            return _payments
                .Where(p =>
                    p.PayerId.Equals(companyId, StringComparison.OrdinalIgnoreCase)
                    || p.BeneficiaryId.Equals(companyId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p.Amount)
                .Take(take)
                .ToList();
        }
    }

    public IReadOnlyList<Company> PickDemoCandidates(int take = 10)
    {
        lock (_gate)
        {
            return _byId.Values
                .Where(c => c.IsActiveClient && c.HasLoan)
                .Where(c => (c.Revenue != null && c.Revenue > 1000) || GroupRevenue(c.GroupId) > 1_000_000_000m)
                .OrderByDescending(c => GroupRevenue(c.GroupId))
                .ThenByDescending(c => c.Revenue ?? 0)
                .Take(take)
                .ToList();
        }
    }
}
