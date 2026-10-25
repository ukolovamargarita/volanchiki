using Volanchiki.Api.Domain;

namespace Volanchiki.Api.Data;

public class NewsHit
{
    public NewsItem Item { get; set; } = new();
    public int Score { get; set; }
}

/// <summary>In-memory лента новостей: дедуп по Id (ссылка/guid), простой текстовый поиск и ранжирование.</summary>
public class NewsStore
{
    readonly Dictionary<string, NewsItem> _byId = new(StringComparer.OrdinalIgnoreCase);
    readonly object _gate = new();

    public int Count
    {
        get { lock (_gate) return _byId.Count; }
    }

    public DateTimeOffset? LastSuccessUtc { get; private set; }
    public DateTimeOffset? LastAttemptUtc { get; private set; }
    public string? LastError { get; private set; }
    public int LiveCount
    {
        get { lock (_gate) return _byId.Values.Count(n => !n.IsSeed); }
    }

    public int BusinessCount
    {
        get { lock (_gate) return _byId.Values.Count(n => n.BusinessRelevant); }
    }

    public void MarkAttempt(string? error = null)
    {
        LastAttemptUtc = DateTimeOffset.UtcNow;
        if (error != null)
            LastError = error;
    }

    public void MarkSuccess()
    {
        LastSuccessUtc = DateTimeOffset.UtcNow;
        LastError = null;
    }

    public int UpsertMany(IEnumerable<NewsItem> items)
    {
        var added = 0;
        var sawLive = false;
        lock (_gate)
        {
            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Id))
                    continue;

                if (!item.IsSeed)
                    sawLive = true;

                if (_byId.TryGetValue(item.Id, out var existing))
                {
                    if (item.PublishedAt >= existing.PublishedAt)
                    {
                        item.FetchedAt = DateTimeOffset.UtcNow;
                        // LLM уже уточнила — не затираем правилами на каждом poll
                        if (existing.ClassificationSource == "llm")
                        {
                            item.Category = existing.Category;
                            item.BusinessRelevant = existing.BusinessRelevant;
                            item.NeedsLlm = false;
                            item.ClassificationSource = "llm";
                            item.RuleCandidates = existing.RuleCandidates;
                        }
                        else if (existing.NeedsLlm)
                        {
                            // ещё в очереди на LLM
                            item.NeedsLlm = true;
                        }

                        _byId[item.Id] = item;
                    }
                }
                else
                {
                    item.FetchedAt = DateTimeOffset.UtcNow;
                    _byId[item.Id] = item;
                    added++;
                }
            }

            // как только пришли живые RSS — убираем офлайн-seed
            if (sawLive)
            {
                foreach (var id in _byId.Values.Where(n => n.IsSeed).Select(n => n.Id).ToList())
                    _byId.Remove(id);
            }

            // не раздуваем память: оставляем ~400 свежих
            if (_byId.Count > 500)
            {
                var drop = _byId.Values
                    .OrderByDescending(n => n.PublishedAt)
                    .Skip(400)
                    .Select(n => n.Id)
                    .ToList();
                foreach (var id in drop)
                    _byId.Remove(id);
            }
        }

        return added;
    }

    public void EnsureSeed(IEnumerable<NewsItem> seeds)
    {
        lock (_gate)
        {
            if (_byId.Values.Any(n => !n.IsSeed))
                return;
            if (_byId.Count > 0)
                return;

            foreach (var s in seeds)
                _byId[s.Id] = s;
        }
    }

    public IReadOnlyList<NewsHit> Search(
        string? query,
        int take = 20,
        string? companyContext = null,
        bool businessOnly = true)
    {
        take = Math.Clamp(take, 1, 50);
        var qTokens = Tokenize(query);
        var cTokens = Tokenize(companyContext);

        lock (_gate)
        {
            var hits = new List<NewsHit>();
            foreach (var n in _byId.Values)
            {
                var score = 0;
                var hay = (n.Title + " " + n.Summary + " " + n.Category + " " + n.Source).ToLowerInvariant();

                foreach (var t in qTokens)
                {
                    if (hay.Contains(t))
                        score += 3;
                    if (n.Title.ToLowerInvariant().Contains(t))
                        score += 2;
                }

                foreach (var t in cTokens)
                {
                    if (hay.Contains(t))
                        score += 2;
                }

                // свежесть: до +5 за последние сутки
                var ageH = (DateTimeOffset.UtcNow - n.PublishedAt).TotalHours;
                if (ageH < 6) score += 5;
                else if (ageH < 24) score += 3;
                else if (ageH < 72) score += 1;

                if (!n.IsSeed) score += 1;
                if (n.BusinessRelevant) score += 4;
                if (n.Category is not "Прочее" and not "Регион") score += 2;

                // без запроса — все; с запросом — только релевантные (или топ по свежести если 0 совпадений)
                if (qTokens.Count > 0 && score < 3 && cTokens.Count == 0)
                    continue;

                hits.Add(new NewsHit { Item = n, Score = score });
            }

            IEnumerable<NewsHit> pool = hits;
            if (businessOnly)
            {
                var biz = hits.Where(h => h.Item.BusinessRelevant).ToList();
                // если экономики мало — не оставляем ленту пустой
                if (biz.Count >= Math.Min(5, take))
                    pool = biz;
                else if (biz.Count > 0)
                    pool = biz.Concat(hits.Where(h => !h.Item.BusinessRelevant)).Take(take * 2);
            }

            var ordered = pool
                .OrderByDescending(h => h.Score)
                .ThenByDescending(h => h.Item.PublishedAt)
                .ToList();

            if (qTokens.Count > 0 && ordered.All(h => h.Score < 3))
            {
                var fallbackSrc = businessOnly
                    ? _byId.Values.Where(n => n.BusinessRelevant)
                    : _byId.Values;
                if (!fallbackSrc.Any())
                    fallbackSrc = _byId.Values;

                return fallbackSrc
                    .OrderByDescending(n => n.PublishedAt)
                    .Take(take)
                    .Select(n => new NewsHit { Item = n, Score = 0 })
                    .ToList();
            }

            return ordered.Take(take).ToList();
        }
    }

    public NewsItem? Get(string id)
    {
        lock (_gate)
            return _byId.TryGetValue(id, out var n) ? n : null;
    }

    public int PendingLlmCount
    {
        get { lock (_gate) return _byId.Values.Count(n => n.NeedsLlm); }
    }

    /// <summary>Забирает до max новостей, ждущих LLM-рубрику (помечает NeedsLlm=false на время обработки).</summary>
    public List<NewsItem> ClaimPendingLlm(int max)
    {
        max = Math.Clamp(max, 1, 50);
        lock (_gate)
        {
            var batch = _byId.Values
                .Where(n => n.NeedsLlm && !n.IsSeed)
                .OrderByDescending(n => n.PublishedAt)
                .Take(max)
                .ToList();

            foreach (var n in batch)
                n.NeedsLlm = false;

            return batch.Select(Clone).ToList();
        }
    }

    public void ApplyLlmClassification(string id, string category, bool businessRelevant)
    {
        lock (_gate)
        {
            if (!_byId.TryGetValue(id, out var n))
                return;

            n.Category = category;
            n.BusinessRelevant = businessRelevant
                || category is not "Прочее" and not "Регион";
            n.NeedsLlm = false;
            n.ClassificationSource = "llm";
        }
    }

    /// <summary>Вернуть в очередь, если LLM не ответила.</summary>
    public void RequeueLlm(IEnumerable<string> ids)
    {
        lock (_gate)
        {
            foreach (var id in ids)
            {
                if (_byId.TryGetValue(id, out var n) && n.ClassificationSource != "llm")
                    n.NeedsLlm = true;
            }
        }
    }

    static NewsItem Clone(NewsItem n) => new()
    {
        Id = n.Id,
        Source = n.Source,
        Title = n.Title,
        Summary = n.Summary,
        Url = n.Url,
        Category = n.Category,
        BusinessRelevant = n.BusinessRelevant,
        NeedsLlm = n.NeedsLlm,
        ClassificationSource = n.ClassificationSource,
        RuleCandidates = n.RuleCandidates.ToList(),
        PublishedAt = n.PublishedAt,
        FetchedAt = n.FetchedAt,
        IsSeed = n.IsSeed,
    };

    static List<string> Tokenize(string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return new List<string>();

        return q.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', ';', ':', '!', '?', '\n', '\r', '\t', '"', '(', ')', '/', '-' },
                StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 3)
            .Distinct()
            .ToList();
    }
}
