using Volanchiki.Api.Data;
using Volanchiki.Api.Domain;
using Xunit;
using static Volanchiki.Tests.TestData;

namespace Volanchiki.Tests;

public class CompanyStoreTests
{
    static CompanyStore Store(IEnumerable<Company> companies, IEnumerable<PaymentEdge>? payments = null)
    {
        var s = new CompanyStore();
        s.ReplaceAll(companies, Array.Empty<PersonLink>(), payments ?? Array.Empty<PaymentEdge>());
        return s;
    }

    [Fact]
    public void Company_lookup_is_case_insensitive()
    {
        var s = Store(new[] { Co("U_1") });

        Assert.NotNull(s.Get("u_1"));
        Assert.Null(s.Get("U_2"));
    }

    [Fact]
    public void Companies_with_empty_id_are_skipped_and_store_is_marked_loaded()
    {
        var s = Store(new[] { Co(""), Co("U_1") });

        Assert.True(s.IsLoaded);
        Assert.Equal(1, s.CompanyCount);
    }

    [Fact]
    public void ReplaceAll_drops_previous_data()
    {
        var s = Store(new[] { Co("U_1") });
        s.ReplaceAll(new[] { Co("U_2") }, Array.Empty<PersonLink>(), Array.Empty<PaymentEdge>());

        Assert.Null(s.Get("U_1"));
        Assert.NotNull(s.Get("U_2"));
    }

    [Fact]
    public void Client_type_check_is_case_insensitive()
    {
        Assert.True(Co("a", "client").IsActiveClient);
        Assert.False(Co("a", "POTENTIAL").IsActiveClient);
    }

    [Fact]
    public void Group_revenue_ignores_missing_and_placeholder_1000_values()
    {
        var s = Store(new[]
        {
            Co("A", revenue: 5_000_000_000m, group: "G1"),
            Co("B", revenue: 1000m, group: "G1"),
            Co("C", revenue: null, group: "G1"),
            Co("D", revenue: 2_000_000_000m, group: "G1"),
        });

        Assert.Equal(7_000_000_000m, s.GroupRevenue("G1"));
    }

    [Fact]
    public void Group_lookup_is_case_insensitive_and_unknown_group_is_empty()
    {
        var s = Store(new[] { Co("A", group: "G1"), Co("B", group: "G1") });

        Assert.Equal(2, s.GetGroup("g1").Count);
        Assert.Empty(s.GetGroup("G404"));
        Assert.Empty(s.GetGroup(null));
        Assert.Equal(0m, s.GroupRevenue(null));
    }

    [Fact]
    public void Search_returns_only_active_clients()
    {
        var s = Store(new[]
        {
            Co("U_1", "CLIENT"),
            Co("U_2", "POTENTIAL"),
        });

        var res = s.SearchClients(null);

        Assert.Single(res);
        Assert.Equal("U_1", res[0].Id);
    }

    [Fact]
    public void Search_matches_okved_city_and_id_ignoring_case()
    {
        var s = Store(new[]
        {
            Co("U_1", okved: "Производство стали", city: "Липецк"),
            Co("U_2", okved: "Розничная торговля", city: "Казань"),
        });

        Assert.Equal("U_1", s.SearchClients("СТАЛИ")[0].Id);
        Assert.Equal("U_2", s.SearchClients("казань")[0].Id);
        Assert.Equal("U_2", s.SearchClients("u_2")[0].Id);
        Assert.Empty(s.SearchClients("нет такого"));
    }

    [Fact]
    public void Search_take_is_clamped()
    {
        var s = Store(Enumerable.Range(0, 150).Select(i => Co("U_" + i)));

        Assert.Equal(1, s.SearchClients(null, 0).Count);
        Assert.Equal(100, s.SearchClients(null, 1000).Count);
    }

    [Fact]
    public void Payments_include_both_directions_sorted_by_amount_desc()
    {
        var s = Store(
            new[] { Co("A") },
            new[]
            {
                new PaymentEdge { PayerId = "A", BeneficiaryId = "X", Amount = 10 },
                new PaymentEdge { PayerId = "Y", BeneficiaryId = "A", Amount = 300 },
                new PaymentEdge { PayerId = "Z", BeneficiaryId = "Q", Amount = 999 },
                new PaymentEdge { PayerId = "A", BeneficiaryId = "W", Amount = 50 },
            });

        var res = s.PaymentsFor("a", 10);

        Assert.Equal(3, res.Count);
        Assert.Equal(300m, res[0].Amount);
        Assert.Equal(50m, res[1].Amount);
        Assert.Equal(10m, res[2].Amount);
    }

    [Fact]
    public void Payments_respect_take_limit()
    {
        var s = Store(
            new[] { Co("A") },
            Enumerable.Range(1, 5).Select(i => new PaymentEdge { PayerId = "A", BeneficiaryId = "B" + i, Amount = i }));

        Assert.Equal(2, s.PaymentsFor("A", 2).Count);
    }

    [Fact]
    public void Demo_candidates_are_active_clients_with_loan_and_meaningful_revenue()
    {
        var s = Store(new[]
        {
            Co("good", revenue: 20_000_000_000m, group: "G1", loan: true),
            Co("no-loan", revenue: 30_000_000_000m, loan: false),
            Co("potential", "POTENTIAL", revenue: 30_000_000_000m, loan: true),
            Co("tiny", revenue: 500m, loan: true),
        });

        var res = s.PickDemoCandidates(10);

        Assert.Single(res);
        Assert.Equal("good", res[0].Id);
    }

    [Fact]
    public void Demo_candidates_are_ordered_by_group_revenue_desc()
    {
        var s = Store(new[]
        {
            Co("small", revenue: 2_000_000_000m, group: "G1", loan: true),
            Co("big", revenue: 9_000_000_000m, group: "G2", loan: true),
        });

        var res = s.PickDemoCandidates(10);

        Assert.Equal("big", res[0].Id);
        Assert.Equal("small", res[1].Id);
    }

    [Fact]
    public void Company_without_own_revenue_qualifies_if_its_group_is_big()
    {
        var s = Store(new[]
        {
            Co("head", revenue: 5_000_000_000m, group: "G1"),
            Co("sub", revenue: null, group: "G1", loan: true),
        });

        var res = s.PickDemoCandidates(10);

        Assert.Single(res);
        Assert.Equal("sub", res[0].Id);
    }
}
