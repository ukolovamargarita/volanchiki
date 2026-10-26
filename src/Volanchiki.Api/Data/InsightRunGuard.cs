using System.Collections.Concurrent;

namespace Volanchiki.Api.Data;

/// <summary>Лимит одновременных прогонов и частоты стартов (защита бюджета LLM).</summary>
public class InsightRunGuard
{
    public const int MaxNewsHintChars = 4000;
    public const int MaxConcurrent = 3;
    public const int MaxStartsPerMinute = 8;

    readonly SemaphoreSlim _slots = new(MaxConcurrent, MaxConcurrent);
    readonly ConcurrentDictionary<string, ConcurrentQueue<long>> _starts = new(StringComparer.OrdinalIgnoreCase);

    public bool TryEnter(string clientKey, out string? error)
    {
        error = null;
        Prune(clientKey);
        var q = _starts.GetOrAdd(clientKey, _ => new ConcurrentQueue<long>());
        if (q.Count >= MaxStartsPerMinute)
        {
            error = "rate_limited";
            return false;
        }

        if (!_slots.Wait(0))
        {
            error = "too_many_concurrent_runs";
            return false;
        }

        q.Enqueue(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        return true;
    }

    public void Exit() => _slots.Release();

    void Prune(string key)
    {
        if (!_starts.TryGetValue(key, out var q))
            return;
        var cutoff = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 60;
        while (q.TryPeek(out var t) && t < cutoff)
            q.TryDequeue(out _);
    }
}
