using System.Collections.Concurrent;
using Volanchiki.Api.Domain;

namespace Volanchiki.Api.Data;

/// <summary>In-memory статусы прогонов агента — чтобы клиент мог опрашивать после обрыва сети.</summary>
public class InsightRunStore
{
    readonly ConcurrentDictionary<string, InsightRunJob> _jobs = new(StringComparer.OrdinalIgnoreCase);

    public InsightRunJob Start(string companyId, string? newsHint)
    {
        var job = new InsightRunJob
        {
            RunId = Guid.NewGuid().ToString("N"),
            CompanyId = companyId,
            NewsHint = newsHint,
            Status = "running",
            StartedAt = DateTimeOffset.UtcNow,
        };
        _jobs[job.RunId] = job;
        Prune();
        return job;
    }

    public InsightRunJob? Get(string runId) =>
        _jobs.TryGetValue(runId, out var j) ? j : null;

    public void Complete(string runId, AgentRunResult result)
    {
        if (!_jobs.TryGetValue(runId, out var job))
            return;

        job.Status = result.Status == "ok" ? "ok" : "error";
        job.Result = result;
        job.FinishedAt = DateTimeOffset.UtcNow;
        // runId в ответе = id джобы (удобно для клиента)
        result.RunId = runId;
    }

    public void Fail(string runId, string error)
    {
        if (!_jobs.TryGetValue(runId, out var job))
            return;

        job.Status = "error";
        job.FinishedAt = DateTimeOffset.UtcNow;
        job.Result = new AgentRunResult
        {
            RunId = runId,
            Status = "error",
            Error = error,
            Trace = new List<AgentTraceStep>(),
        };
    }

    void Prune()
    {
        if (_jobs.Count < 200)
            return;

        var cutoff = DateTimeOffset.UtcNow.AddHours(-2);
        foreach (var kv in _jobs)
        {
            var t = kv.Value.FinishedAt ?? kv.Value.StartedAt;
            if (t < cutoff)
                _jobs.TryRemove(kv.Key, out _);
        }
    }
}

public class InsightRunJob
{
    public string RunId { get; set; } = "";
    public string CompanyId { get; set; } = "";
    public string? NewsHint { get; set; }
    public string Status { get; set; } = "running"; // running | ok | error
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public AgentRunResult? Result { get; set; }
}
