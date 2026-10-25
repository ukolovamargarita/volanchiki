using Volanchiki.Api.Data;
using Xunit;

namespace Volanchiki.Tests;

public class AlfaAnalyticsStoreTests
{
    static AlfaAnalyticsStore Store()
    {
        var s = new AlfaAnalyticsStore();
        s.ReplaceAll(new[]
        {
            new AlfaDoc
            {
                FileName = "rate.txt",
                Title = "Ключевая ставка ЦБ",
                Text = "Ставка без изменения на 14%. Риторика регулятора стала жёстче.",
            },
            new AlfaDoc
            {
                FileName = "gold.txt",
                Title = "Обзор рынка золота",
                Text = "Золото подорожало в августе на фоне спроса.",
            },
        });
        return s;
    }

    [Fact]
    public void Is_not_loaded_until_documents_are_added()
    {
        var s = new AlfaAnalyticsStore();
        Assert.False(s.IsLoaded);

        s.ReplaceAll(Array.Empty<AlfaDoc>());
        Assert.False(s.IsLoaded);

        s.ReplaceAll(new[] { new AlfaDoc { FileName = "a.txt", Text = "x" } });
        Assert.True(s.IsLoaded);
        Assert.Equal(1, s.DocCount);
    }

    [Fact]
    public void Finds_document_by_title_and_text()
    {
        var hits = Store().Search("ключевая ставка");

        Assert.Equal("rate.txt", hits[0].FileName);
    }

    [Fact]
    public void Returns_only_documents_that_contain_the_word()
    {
        var hits = Store().Search("золото");

        Assert.Single(hits);
        Assert.Equal("gold.txt", hits[0].FileName);
    }

    [Fact]
    public void Search_is_case_insensitive()
    {
        Assert.Single(Store().Search("СТАВКА"));
    }

    [Fact]
    public void No_match_gives_empty_result()
    {
        Assert.Empty(Store().Search("криптовалюта"));
    }

    [Fact]
    public void Empty_query_returns_documents_with_zero_score()
    {
        var hits = Store().Search(null, 5);

        Assert.Equal(2, hits.Count);
        Assert.Equal(0, hits[0].Score);
    }

    [Fact]
    public void Document_matching_more_words_ranks_higher()
    {
        var s = new AlfaAnalyticsStore();
        s.ReplaceAll(new[]
        {
            new AlfaDoc { FileName = "one.txt", Title = "a", Text = "инфляция" },
            new AlfaDoc { FileName = "two.txt", Title = "b", Text = "инфляция и ставка и кредиты" },
        });

        var hits = s.Search("инфляция ставка кредиты");

        Assert.Equal("two.txt", hits[0].FileName);
    }

    [Fact]
    public void Excerpt_is_taken_around_the_match_and_is_short()
    {
        var text = new string('х', 2000) + " золото подорожало " + new string('у', 2000);
        var s = new AlfaAnalyticsStore();
        s.ReplaceAll(new[] { new AlfaDoc { FileName = "long.txt", Title = "t", Text = text } });

        var hit = s.Search("золото")[0];

        Assert.Contains("золото", hit.Excerpt);
        Assert.True(hit.Excerpt.Length <= 460);
    }

    [Fact]
    public void Take_is_clamped_to_at_least_one()
    {
        Assert.Equal(1, Store().Search(null, 0).Count);
    }
}
