using Volanchiki.Api.Data;
using Xunit;
using static Volanchiki.Tests.TestData;

namespace Volanchiki.Tests;

public class NewsStoreTests
{
    static DateTimeOffset Ago(double hours) => DateTimeOffset.UtcNow.AddHours(-hours);

    [Fact]
    public void Same_id_is_stored_once_and_reports_zero_added_second_time()
    {
        var store = new NewsStore();

        Assert.Equal(1, store.UpsertMany(new[] { News("a") }));
        Assert.Equal(0, store.UpsertMany(new[] { News("a") }));
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public void Newer_version_replaces_older()
    {
        var store = new NewsStore();
        store.UpsertMany(new[] { News("a", "старый", published: Ago(10)) });
        store.UpsertMany(new[] { News("a", "новый", published: Ago(1)) });

        Assert.Equal("новый", store.Get("a")!.Title);
    }

    [Fact]
    public void Older_version_does_not_replace_newer()
    {
        var store = new NewsStore();
        store.UpsertMany(new[] { News("a", "новый", published: Ago(1)) });
        store.UpsertMany(new[] { News("a", "старый", published: Ago(10)) });

        Assert.Equal("новый", store.Get("a")!.Title);
    }

    [Fact]
    public void Items_with_empty_id_are_ignored()
    {
        var store = new NewsStore();
        store.UpsertMany(new[] { News(""), News("  ") });

        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Seed_is_removed_when_live_news_arrive()
    {
        var store = new NewsStore();
        store.EnsureSeed(new[] { News("s1", seed: true), News("s2", seed: true) });
        Assert.Equal(2, store.Count);

        store.UpsertMany(new[] { News("live1") });

        Assert.Equal(1, store.Count);
        Assert.Equal(1, store.LiveCount);
        Assert.Null(store.Get("s1"));
    }

    [Fact]
    public void Seed_items_alone_do_not_remove_other_seeds()
    {
        var store = new NewsStore();
        store.UpsertMany(new[] { News("s1", seed: true), News("s2", seed: true) });

        Assert.Equal(2, store.Count);
        Assert.Equal(0, store.LiveCount);
    }

    [Fact]
    public void EnsureSeed_does_nothing_when_live_news_exist()
    {
        var store = new NewsStore();
        store.UpsertMany(new[] { News("live1") });
        store.EnsureSeed(new[] { News("s1", seed: true) });

        Assert.Null(store.Get("s1"));
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public void Search_ranks_relevant_item_first()
    {
        var store = new NewsStore();
        store.UpsertMany(new[]
        {
            News("foot", "Футбол: итоги тура", published: Ago(2)),
            News("rate", "Ключевая ставка сохранена", published: Ago(2)),
        });

        var hits = store.Search("ставка");

        Assert.Equal("rate", hits[0].Item.Id);
    }

    [Fact]
    public void Search_drops_old_irrelevant_items_when_query_matches_something()
    {
        var store = new NewsStore();
        store.UpsertMany(new[]
        {
            News("old-foot", "Футбол: итоги тура", published: Ago(24 * 10)),
            News("rate", "Ключевая ставка сохранена", published: Ago(24 * 10)),
        });

        var hits = store.Search("ставка");

        Assert.Single(hits);
        Assert.Equal("rate", hits[0].Item.Id);
    }

    [Fact]
    public void Search_with_no_matches_returns_freshest_instead_of_nothing()
    {
        var store = new NewsStore();
        store.UpsertMany(new[]
        {
            News("older", "Футбол", published: Ago(24 * 10)),
            News("newer", "Хоккей", published: Ago(24 * 5)),
        });

        var hits = store.Search("несуществующеесловоqwerty");

        Assert.Equal(2, hits.Count);
        Assert.Equal("newer", hits[0].Item.Id);
    }

    [Fact]
    public void Without_query_fresher_news_come_first()
    {
        var store = new NewsStore();
        store.UpsertMany(new[]
        {
            News("old", published: Ago(100)),
            News("fresh", published: Ago(1)),
        });

        Assert.Equal("fresh", store.Search(null)[0].Item.Id);
    }

    [Fact]
    public void Company_context_boosts_matching_news()
    {
        var store = new NewsStore();
        var t = Ago(1);
        store.UpsertMany(new[]
        {
            News("foot", "Футбол: итоги тура", published: t),
            News("metal", "Металлургия: цены на сталь", published: t),
        });

        var hits = store.Search(null, 5, "производство стали и металлургия");

        Assert.Equal("metal", hits[0].Item.Id);
    }

    [Fact]
    public void Take_is_clamped_between_1_and_50()
    {
        var store = new NewsStore();
        store.UpsertMany(Enumerable.Range(0, 60).Select(i => News("n" + i, published: Ago(i + 1))));

        Assert.Equal(1, store.Search(null, 0).Count);
        Assert.Equal(50, store.Search(null, 1000).Count);
    }

    [Fact]
    public void Store_is_trimmed_to_400_freshest_after_exceeding_500()
    {
        var store = new NewsStore();
        store.UpsertMany(Enumerable.Range(0, 501).Select(i => News("n" + i, published: Ago(i / 60.0))));

        Assert.Equal(400, store.Count);
        Assert.NotNull(store.Get("n0"));
        Assert.Null(store.Get("n500"));
    }

    [Fact]
    public void Success_clears_last_error()
    {
        var store = new NewsStore();
        store.MarkAttempt("feed:500");
        Assert.Equal("feed:500", store.LastError);

        store.MarkSuccess();

        Assert.Null(store.LastError);
        Assert.NotNull(store.LastSuccessUtc);
    }
}
