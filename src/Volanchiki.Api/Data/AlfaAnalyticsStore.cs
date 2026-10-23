namespace Volanchiki.Api.Data;

public class AlfaDoc
{
    public string FileName { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
}

public class AlfaHit
{
    public string FileName { get; set; } = "";
    public string Title { get; set; } = "";
    public string Excerpt { get; set; } = "";
    public int Score { get; set; }
}

public class AlfaAnalyticsStore
{
    readonly List<AlfaDoc> _docs = new();
    readonly object _gate = new();

    public bool IsLoaded { get; private set; }
    public int DocCount => _docs.Count;

    public void ReplaceAll(IEnumerable<AlfaDoc> docs)
    {
        lock (_gate)
        {
            _docs.Clear();
            _docs.AddRange(docs);
            IsLoaded = _docs.Count > 0;
        }
    }

    public IReadOnlyList<AlfaDoc> All()
    {
        lock (_gate)
            return _docs.ToList();
    }

    // простой поиск по словам — для хакатона хватает
    public IReadOnlyList<AlfaHit> Search(string? query, int take = 3)
    {
        take = Math.Clamp(take, 1, 10);
        var tokens = Tokenize(query);
        if (tokens.Count == 0)
        {
            lock (_gate)
            {
                return _docs.Take(take).Select(d => new AlfaHit
                {
                    FileName = d.FileName,
                    Title = d.Title,
                    Excerpt = Clip(d.Text, 500),
                    Score = 0,
                }).ToList();
            }
        }

        lock (_gate)
        {
            var hits = new List<AlfaHit>();
            foreach (var d in _docs)
            {
                var lower = d.Text.ToLowerInvariant();
                var score = 0;
                foreach (var t in tokens)
                {
                    if (lower.Contains(t))
                        score += 1;
                    if (d.Title.ToLowerInvariant().Contains(t) || d.FileName.ToLowerInvariant().Contains(t))
                        score += 2;
                }

                if (score == 0)
                    continue;

                hits.Add(new AlfaHit
                {
                    FileName = d.FileName,
                    Title = d.Title,
                    Excerpt = BestExcerpt(d.Text, tokens, 450),
                    Score = score,
                });
            }

            return hits.OrderByDescending(h => h.Score).Take(take).ToList();
        }
    }

    static List<string> Tokenize(string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return new List<string>();

        return q.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', ';', ':', '!', '?', '\n', '\r', '\t', '"', '(', ')' },
                StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 3)
            .Distinct()
            .ToList();
    }

    static string BestExcerpt(string text, List<string> tokens, int maxLen)
    {
        var lower = text.ToLowerInvariant();
        var idx = -1;
        foreach (var t in tokens)
        {
            idx = lower.IndexOf(t, StringComparison.Ordinal);
            if (idx >= 0)
                break;
        }

        if (idx < 0)
            return Clip(text, maxLen);

        var start = Math.Max(0, idx - 80);
        return Clip(text.Substring(start), maxLen);
    }

    static string Clip(string s, int max)
    {
        s = s.Replace("\r\n", "\n").Trim();
        if (s.Length <= max)
            return s;
        return s.Substring(0, max).Trim() + "...";
    }
}
