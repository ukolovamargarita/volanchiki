using Volanchiki.Api.Data;
using Xunit;
using static Volanchiki.Tests.TestData;

namespace Volanchiki.Tests;

// Очередь доклассификации моделью и фильтр «только бизнес» в ленте.
public class NewsStoreLlmQueueTests
{
    static DateTimeOffset Ago(double hours) => DateTimeOffset.UtcNow.AddHours(-hours);

    static NewsStore WithPending(params string[] ids)
    {
        var s = new NewsStore();
        s.UpsertMany(ids.Select((id, i) => News(id, "Новость " + id, published: Ago(i + 1), needsLlm: true)));
        return s;
    }

    // ---------- очередь ----------

    [Fact]
    public void Pending_count_includes_only_items_waiting_for_the_model()
    {
        var s = new NewsStore();
        s.UpsertMany(new[] { News("a", needsLlm: true), News("b"), News("c", needsLlm: true) });

        Assert.Equal(2, s.PendingLlmCount);
    }

    [Fact]
    public void Claim_returns_newest_first_and_removes_them_from_the_queue()
    {
        var s = WithPending("fresh", "mid", "old");

        var batch = s.ClaimPendingLlm(2);

        Assert.Equal(2, batch.Count);
        Assert.Equal("fresh", batch[0].Id);
        Assert.Equal("mid", batch[1].Id);
        Assert.Equal(1, s.PendingLlmCount);
    }

    [Fact]
    public void Claim_returns_copies_so_the_caller_cannot_change_stored_news()
    {
        var s = WithPending("a");

        var batch = s.ClaimPendingLlm(5);
        batch[0].Title = "ИЗМЕНЕНО";

        Assert.Equal("Новость a", s.Get("a")!.Title);
    }

    [Fact]
    public void Claim_never_takes_demo_seed_items()
    {
        var s = new NewsStore();
        s.UpsertMany(new[] { News("seed", seed: true, needsLlm: true) });

        Assert.Empty(s.ClaimPendingLlm(5));
    }

    [Fact]
    public void Claim_size_is_clamped_between_1_and_50()
    {
        var s = new NewsStore();
        s.UpsertMany(Enumerable.Range(0, 60).Select(i => News("n" + i, published: Ago(i + 1), needsLlm: true)));

        Assert.Equal(1, s.ClaimPendingLlm(0).Count);
        Assert.Equal(50, s.ClaimPendingLlm(1000).Count);
    }

    // ---------- применение ответа ----------

    [Fact]
    public void Apply_sets_category_and_marks_the_item_as_classified_by_the_model()
    {
        var s = WithPending("a");

        s.ApplyLlmClassification("a", "Нефть", false);

        var n = s.Get("a")!;
        Assert.Equal("Нефть", n.Category);
        Assert.Equal("llm", n.ClassificationSource);
        Assert.False(n.NeedsLlm);
    }

    [Fact]
    public void Apply_makes_any_real_category_business_relevant_even_if_the_model_said_no()
    {
        var s = WithPending("a");

        s.ApplyLlmClassification("a", "Нефть", false);

        Assert.True(s.Get("a")!.BusinessRelevant);
    }

    [Fact]
    public void Apply_keeps_other_non_business_when_model_says_so()
    {
        var s = WithPending("a");

        s.ApplyLlmClassification("a", "Прочее", false);

        Assert.False(s.Get("a")!.BusinessRelevant);
        Assert.Equal("llm", s.Get("a")!.ClassificationSource);
    }

    [Fact]
    public void Apply_for_unknown_id_does_nothing()
    {
        var s = WithPending("a");

        s.ApplyLlmClassification("ghost", "Нефть", true);

        Assert.Null(s.Get("ghost"));
        Assert.Equal(1, s.Count);
    }

    // ---------- возврат в очередь ----------

    [Fact]
    public void Requeue_puts_unanswered_items_back()
    {
        var s = WithPending("a");
        s.ClaimPendingLlm(5);
        Assert.Equal(0, s.PendingLlmCount);

        s.RequeueLlm(new[] { "a" });

        Assert.Equal(1, s.PendingLlmCount);
    }

    [Fact]
    public void Requeue_does_not_undo_a_model_decision()
    {
        var s = WithPending("a");
        s.ApplyLlmClassification("a", "Нефть", true);

        s.RequeueLlm(new[] { "a" });

        Assert.Equal(0, s.PendingLlmCount);
        Assert.Equal("Нефть", s.Get("a")!.Category);
    }

    [Fact]
    public void Requeue_ignores_unknown_ids()
    {
        var s = WithPending("a");

        s.RequeueLlm(new[] { "ghost" });

        Assert.Equal(1, s.PendingLlmCount);
    }

    // ---------- повторное получение той же новости ----------

    [Fact]
    public void Refetching_a_news_item_does_not_overwrite_the_models_category()
    {
        var s = WithPending("a");
        s.ApplyLlmClassification("a", "Нефть", true);

        // на следующем опросе лента снова отдала ту же новость, правила снова сказали «Прочее»
        var again = News("a", "Новость a", published: DateTimeOffset.UtcNow, needsLlm: true);
        s.UpsertMany(new[] { again });

        var n = s.Get("a")!;
        Assert.Equal("Нефть", n.Category);
        Assert.False(n.NeedsLlm);
        Assert.Equal("llm", n.ClassificationSource);
    }

    [Fact]
    public void Refetching_an_item_that_is_still_waiting_keeps_it_in_the_queue()
    {
        var s = WithPending("a");

        var again = News("a", "Новость a", published: DateTimeOffset.UtcNow, needsLlm: false);
        s.UpsertMany(new[] { again });

        Assert.True(s.Get("a")!.NeedsLlm);
    }

    // ---------- фильтр «только бизнес» ----------

    [Fact]
    public void Business_count_counts_relevant_items()
    {
        var s = new NewsStore();
        s.UpsertMany(new[] { News("a", business: true), News("b"), News("c", business: true) });

        Assert.Equal(2, s.BusinessCount);
    }

    [Fact]
    public void Feed_shows_only_business_news_when_there_are_enough_of_them()
    {
        var s = new NewsStore();
        s.UpsertMany(Enumerable.Range(0, 6).Select(i => News("biz" + i, published: Ago(1), business: true)));
        s.UpsertMany(Enumerable.Range(0, 3).Select(i => News("fun" + i, published: Ago(1))));

        var hits = s.Search(null, 5);

        Assert.Equal(5, hits.Count);
        Assert.False(hits.Any(h => !h.Item.BusinessRelevant));
    }

    [Fact]
    public void Feed_includes_non_business_news_when_business_ones_are_not_enough()
    {
        var s = new NewsStore();
        s.UpsertMany(new[] { News("biz1", published: Ago(1), business: true), News("biz2", published: Ago(1), business: true) });
        s.UpsertMany(Enumerable.Range(0, 5).Select(i => News("fun" + i, published: Ago(1))));

        var hits = s.Search(null, 5);

        Assert.True(hits.Count > 2);
        Assert.True(hits.Any(h => !h.Item.BusinessRelevant));
        Assert.Equal(2, hits.Count(h => h.Item.BusinessRelevant));
    }

    [Fact]
    public void Feed_is_not_empty_when_no_news_are_business_relevant()
    {
        var s = new NewsStore();
        s.UpsertMany(new[] { News("a", published: Ago(1)), News("b", published: Ago(2)) });

        Assert.Equal(2, s.Search(null, 5).Count);
    }

    [Fact]
    public void Business_filter_can_be_switched_off()
    {
        var s = new NewsStore();
        s.UpsertMany(Enumerable.Range(0, 6).Select(i => News("biz" + i, published: Ago(1), business: true)));
        s.UpsertMany(new[] { News("fun", "Футбол", published: Ago(1)) });

        var hits = s.Search(null, 20, businessOnly: false);

        Assert.Equal(7, hits.Count);
    }

    [Fact]
    public void Business_news_rank_above_equally_fresh_non_business_news()
    {
        var s = new NewsStore();
        var t = Ago(1);
        s.UpsertMany(new[] { News("fun", published: t), News("biz", published: t, business: true) });

        var hits = s.Search(null, 5, businessOnly: false);

        Assert.Equal("biz", hits[0].Item.Id);
    }
}
