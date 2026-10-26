using Volanchiki.Api.News;
using Xunit;

namespace Volanchiki.Tests;

// Правила дают provisional + кандидатов; NeedsLlm всегда true — рубрику ставит LLM.
public class NewsClassifierTests
{
    [Theory]
    [InlineData("Банк России сохранил ключевую ставку на уровне 14%", "Ставка")]
    [InlineData("Ключевая ставка выросла", "Ставка")]
    [InlineData("КЛЮЧЕВАЯ СТАВКА СОХРАНЕНА", "Ставка")]
    [InlineData("Росстат: недельная инфляция замедлилась", "Инфляция")]
    [InlineData("Рост ВВП в первом полугодии оказался слабым", "Макро")]
    [InlineData("Цены на нефть Brent выросли", "Нефть")]
    [InlineData("Золото обновило исторический максимум", "Металлы")]
    [InlineData("Мосбиржа запустила торги облигациями", "Финансы")]
    [InlineData("США ввели новые санкции", "Санкции")]
    [InlineData("Выручка компании выросла на 20%", "Бизнес")]
    [InlineData("Введены пошлины на импорт", "Торговля")]
    public void Clear_headline_gets_provisional_category_and_always_asks_the_model(string title, string expected)
    {
        var r = NewsClassifier.Evaluate(title, "");

        Assert.Equal(expected, r.Category);
        Assert.True(r.NeedsLlm);
        Assert.Equal("rules_hint", r.Source);
        Assert.Single(r.MatchedTags);
        Assert.True(r.BusinessRelevant);
    }

    [Fact]
    public void Region_news_is_provisional_not_business_and_goes_to_model()
    {
        var r = NewsClassifier.Evaluate("Губернатор области открыл новый завод", "");

        Assert.Equal("Регион", r.Category);
        Assert.False(r.BusinessRelevant);
        Assert.True(r.NeedsLlm);
    }

    [Theory]
    [InlineData("Концерт в городском парке")]
    [InlineData("Футбольный клуб выиграл матч")]
    [InlineData("Курсовая работа студента получила награду")]
    [InlineData("Европейский парламент принял закон")]
    [InlineData("")]
    public void Unmatched_text_is_other_and_goes_to_the_model(string title)
    {
        var r = NewsClassifier.Evaluate(title, "");

        Assert.Equal("Прочее", r.Category);
        Assert.True(r.NeedsLlm);
        Assert.Equal("rules_other", r.Source);
        Assert.Empty(r.MatchedTags);
        Assert.False(r.BusinessRelevant);
    }

    [Fact]
    public void Two_matching_categories_go_to_the_model_with_both_as_candidates()
    {
        var r = NewsClassifier.Evaluate("Банк России и Росстат обсудили инфляцию", "");

        Assert.True(r.NeedsLlm);
        Assert.Equal("rules_ambiguous", r.Source);
        Assert.Equal(2, r.MatchedTags.Count);
        Assert.Contains("Ставка", r.MatchedTags);
        Assert.Contains("Инфляция", r.MatchedTags);
        Assert.Equal("Ставка", r.Category);
    }

    [Fact]
    public void Summary_is_used_when_title_is_neutral()
    {
        var r = NewsClassifier.Evaluate("Новости дня", "Росстат опубликовал данные");

        Assert.Equal("Инфляция", r.Category);
        Assert.True(r.NeedsLlm);
    }

    [Theory]
    [InlineData("Газ подорожал на бирже", "Нефть")]
    [InlineData("Евро подорожал к рублю", "Валюта")]
    [InlineData("Медь подорожала на 5%", "Металлы")]
    public void Short_ambiguous_words_match_as_whole_words(string title, string expected)
    {
        var r = NewsClassifier.Evaluate(title, "");
        Assert.Equal(expected, r.Category);
        Assert.True(r.NeedsLlm);
    }

    [Theory]
    [InlineData("Газета опубликовала интервью")]
    [InlineData("Европейский союз обсудил новый закон")]
    public void Short_ambiguous_words_do_not_match_inside_longer_words(string title)
    {
        var r = NewsClassifier.Evaluate(title, "");

        Assert.Equal("Прочее", r.Category);
        Assert.True(r.NeedsLlm);
    }

    [Fact]
    public void Business_hint_keeps_unmatched_economic_text_relevant_for_the_feed()
    {
        var r = NewsClassifier.Evaluate("Компания привлекла 5 млрд рублей", "");

        Assert.Equal("Прочее", r.Category);
        Assert.True(r.NeedsLlm);
        Assert.True(r.BusinessRelevant);
    }

    [Fact]
    public void Classify_is_a_shortcut_for_evaluate()
    {
        var full = NewsClassifier.Evaluate("Росстат: недельная инфляция замедлилась", "");
        var (category, business) = NewsClassifier.Classify("Росстат: недельная инфляция замедлилась", "");

        Assert.Equal(full.Category, category);
        Assert.Equal(full.BusinessRelevant, business);
    }

    [Theory]
    [InlineData("Ставка", true)]
    [InlineData("ставка", true)]
    [InlineData("Прочее", true)]
    [InlineData("Выдумка", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void IsAllowed_accepts_only_known_categories(string? category, bool expected)
    {
        Assert.Equal(expected, NewsClassifier.IsAllowed(category));
    }

    [Fact]
    public void Allowed_categories_are_unique_and_include_other()
    {
        var all = NewsClassifier.AllowedCategories;

        Assert.Equal(all.Length, all.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains("Прочее", all);
    }

    [Fact]
    public void False_positive_rule_hit_still_goes_to_the_model()
    {
        // «акци» внутри «Реакция» — provisional может быть Финансы, но LLM всё равно уточнит
        var r = NewsClassifier.Evaluate("Реакция зрителей на премьеру была тёплой", "");

        Assert.True(r.NeedsLlm);
    }
}
