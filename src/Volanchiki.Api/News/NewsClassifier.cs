namespace Volanchiki.Api.News;

/// <summary>
/// Правила → однозначная рубрика.
/// Несколько рубрик или «Прочее» → NeedsLlm (добивает лёгкая модель).
/// </summary>
public static class NewsClassifier
{
    public static readonly string[] AllowedCategories =
    {
        "Ставка", "Инфляция", "Макро", "Валюта", "Нефть", "Металлы",
        "Финансы", "Санкции", "Бизнес", "Торговля", "Регион", "Прочее",
    };

    static readonly Rule[] Rules =
    {
        new("Ставка", true, "ключевая ставка", "ключ ставка", "банк россии", "цб рф", "регулятор", "процентн ставка"),
        new("Инфляция", true, "инфляц", "росстат", "потребительск цен", "ипц"),
        new("Макро", true, "ввп", "макроэкономи", "безработиц", "рынок труда", "промышленн производ", "пмэм"),
        new("Валюта", true, Word("доллар"), Word("евро"), Word("юань"), "курс рубл", "валютн", "валютн курс", "обменн курс"),
        new("Нефть", true, "нефть", "brent", "urals", "газпром", "спг", Word("газ")),
        new("Металлы", true, "сталь", "металл", "золото", "алюмин", "никел", Word("медь")),
        new("Финансы", true, "облигац", "мосбирж", "фондов рынок", "депозит", "кредитн", "банковск", "акци"),
        new("Санкции", true, "санкц", "санкцион", "экспортн контроль", "sdn"),
        new("Бизнес", true, "выручк", "прибыл", "убытк", "дивиденд", "инвестиц", "ipo", "м&a", "сделк", "банкрот"),
        new("Торговля", true, "импорт", "экспорт", "пошлин", "тариф", "ритейл", "оборот розниц"),
        new("Регион", false, "губернатор", "глава регион"),
    };

    static readonly string[] BusinessHints =
    {
        "экономик", "бизнес", "рынок", "компани", "предприят", "бюджет", "налог", "цб",
        "млрд", "млн руб", "выручк", "прибыл", "инфляц", "ставк", "кредит", "депозит",
        "акци", "облигац", "нефть", "газ", "экспорт", "импорт", "инвестиц", "банк",
    };

    public static ClassifyResult Evaluate(string title, string summary)
    {
        var hay = Normalize(title + " " + summary);
        var matched = new List<string>();

        foreach (var rule in Rules)
        {
            if (rule.Keys.Any(k => Matches(hay, k)))
                matched.Add(rule.Tag);
        }

        matched = matched.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (matched.Count == 1)
        {
            var tag = matched[0];
            var business = Rules.First(r => r.Tag == tag).Business;
            return new ClassifyResult(tag, business, NeedsLlm: false, matched, "rules");
        }

        if (matched.Count > 1)
        {
            // временная метка — первая по приоритету правил; LLM уточнит
            var provisional = matched[0];
            var business = matched.Any(t => Rules.First(r => r.Tag == t).Business);
            return new ClassifyResult(provisional, business, NeedsLlm: true, matched, "rules_ambiguous");
        }

        // ничего не совпало
        var hintBiz = BusinessHints.Any(h => Matches(hay, h));
        return new ClassifyResult("Прочее", hintBiz, NeedsLlm: true, matched, "rules_other");
    }

    /// <summary>Совместимость: однозначный ответ без LLM-флага.</summary>
    public static (string Category, bool BusinessRelevant) Classify(string title, string summary)
    {
        var r = Evaluate(title, summary);
        return (r.Category, r.BusinessRelevant);
    }

    public static bool IsAllowed(string? category) =>
        !string.IsNullOrWhiteSpace(category)
        && AllowedCategories.Contains(category, StringComparer.OrdinalIgnoreCase);

    static string Normalize(string s) =>
        s.ToLowerInvariant().Replace('ё', 'е');

    static bool Matches(string hay, Key key)
    {
        if (key.WordOnly || key.Text.Length <= 3)
            return IsWholeWord(hay, key.Text);
        return hay.Contains(key.Text, StringComparison.Ordinal);
    }

    static bool IsWholeWord(string hay, string word)
    {
        var idx = 0;
        while ((idx = hay.IndexOf(word, idx, StringComparison.Ordinal)) >= 0)
        {
            var beforeOk = idx == 0 || !IsWordChar(hay[idx - 1]);
            var afterOk = idx + word.Length >= hay.Length || !IsWordChar(hay[idx + word.Length]);
            if (beforeOk && afterOk)
                return true;
            idx += word.Length;
        }

        return false;
    }

    static bool IsWordChar(char c) => char.IsLetterOrDigit(c);

    static Key Word(string text) => new(text, true);

    readonly record struct Key(string Text, bool WordOnly = false)
    {
        public static implicit operator Key(string text) => new(text);
    }

    sealed class Rule
    {
        public string Tag { get; }
        public bool Business { get; }
        public Key[] Keys { get; }

        public Rule(string tag, bool business, params Key[] keys)
        {
            Tag = tag;
            Business = business;
            Keys = keys;
        }
    }
}

public sealed record ClassifyResult(
    string Category,
    bool BusinessRelevant,
    bool NeedsLlm,
    IReadOnlyList<string> MatchedTags,
    string Source);
