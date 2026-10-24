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
                    // обновляем заголовок/саммари, если пришло свежее
                    if (item.PublishedAt >= existing.PublishedAt)
                    {
                        item.FetchedAt = DateTimeOffset.UtcNow;
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

    public IReadOnlyList<NewsHit> Search(string? query, int take = 20, string? companyContext = null)
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

                // без запроса — все; с запросом — только релевантные (или топ по свежести если 0 совпадений)
                if (qTokens.Count > 0 && score < 3 && cTokens.Count == 0)
                    continue;

                hits.Add(new NewsHit { Item = n, Score = score });
            }

            IEnumerable<NewsHit> ordered = hits
                .OrderByDescending(h => h.Score)
                .ThenByDescending(h => h.Item.PublishedAt);

            if (qTokens.Count > 0 && hits.All(h => h.Score < 3))
            {
                // запрос ничего не нашёл — отдать свежие
                ordered = _byId.Values
                    .OrderByDescending(n => n.PublishedAt)
                    .Take(take)
                    .Select(n => new NewsHit { Item = n, Score = 0 });
                return ordered.ToList();
            }

            return ordered.Take(take).ToList();
        }
    }

    public NewsItem? Get(string id)
    {
        lock (_gate)
            return _byId.TryGetValue(id, out var n) ? n : null;
    }

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
