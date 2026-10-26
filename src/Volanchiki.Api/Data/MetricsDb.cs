using Microsoft.Data.Sqlite;

namespace Volanchiki.Api.Data;

/// <summary>Метрики качества и нагрузки в SQLite (переживают рестарт при volume).</summary>
public class MetricsDb
{
    readonly string _cs;
    readonly object _gate = new();
    readonly ILogger<MetricsDb> _log;

    public MetricsDb(IConfiguration cfg, ILogger<MetricsDb> log)
    {
        _log = log;
        var path = Environment.GetEnvironmentVariable("METRICS_DB_PATH")
                   ?? cfg["Metrics:DbPath"]
                   ?? "data/metrics.db";
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        _cs = new SqliteConnectionStringBuilder { DataSource = full }.ToString();
        EnsureSchema();
        _log.LogInformation("metrics db: {Path}", full);
    }

    public string DbPath
    {
        get
        {
            using var c = Open();
            return Path.GetFullPath(c.DataSource);
        }
    }

    void EnsureSchema()
    {
        using var c = Open();
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS insight_runs (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              run_id TEXT NOT NULL,
              company_id TEXT NOT NULL,
              status TEXT NOT NULL,
              is_clarify INTEGER NOT NULL DEFAULT 0,
              duration_ms INTEGER NOT NULL DEFAULT 0,
              sources_count INTEGER NOT NULL DEFAULT 0,
              has_news INTEGER NOT NULL DEFAULT 0,
              has_alfa INTEGER NOT NULL DEFAULT 0,
              self_check TEXT,
              self_check_retried INTEGER NOT NULL DEFAULT 0,
              created_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_runs_created ON insight_runs(created_at);
            CREATE TABLE IF NOT EXISTS insight_votes (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              run_id TEXT NOT NULL,
              vote TEXT NOT NULL,
              created_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS news_classify_batches (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              applied INTEGER NOT NULL,
              pending_after INTEGER NOT NULL,
              created_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS rate_limit_hits (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              reason TEXT NOT NULL,
              created_at TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public void RecordRun(
        string runId,
        string companyId,
        string status,
        bool isClarify,
        long durationMs,
        int sourcesCount,
        bool hasNews,
        bool hasAlfa,
        string? selfCheck,
        bool selfCheckRetried)
    {
        try
        {
            lock (_gate)
            {
                using var c = Open();
                c.Open();
                using var cmd = c.CreateCommand();
                cmd.CommandText =
                    """
                    INSERT INTO insight_runs
                    (run_id, company_id, status, is_clarify, duration_ms, sources_count,
                     has_news, has_alfa, self_check, self_check_retried, created_at)
                    VALUES ($run, $co, $st, $cl, $dur, $src, $hn, $ha, $sc, $scr, $at)
                    """;
                cmd.Parameters.AddWithValue("$run", runId);
                cmd.Parameters.AddWithValue("$co", companyId);
                cmd.Parameters.AddWithValue("$st", status);
                cmd.Parameters.AddWithValue("$cl", isClarify ? 1 : 0);
                cmd.Parameters.AddWithValue("$dur", durationMs);
                cmd.Parameters.AddWithValue("$src", sourcesCount);
                cmd.Parameters.AddWithValue("$hn", hasNews ? 1 : 0);
                cmd.Parameters.AddWithValue("$ha", hasAlfa ? 1 : 0);
                cmd.Parameters.AddWithValue("$sc", (object?)selfCheck ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$scr", selfCheckRetried ? 1 : 0);
                cmd.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O"));
                cmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "metrics: RecordRun failed");
        }
    }

    public void RecordVote(string runId, string vote)
    {
        try
        {
            lock (_gate)
            {
                using var c = Open();
                c.Open();
                using var cmd = c.CreateCommand();
                cmd.CommandText =
                    "INSERT INTO insight_votes (run_id, vote, created_at) VALUES ($r, $v, $at)";
                cmd.Parameters.AddWithValue("$r", runId);
                cmd.Parameters.AddWithValue("$v", vote);
                cmd.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O"));
                cmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "metrics: RecordVote failed");
        }
    }

    public void RecordNewsClassify(int applied, int pendingAfter)
    {
        if (applied <= 0 && pendingAfter <= 0) return;
        try
        {
            lock (_gate)
            {
                using var c = Open();
                c.Open();
                using var cmd = c.CreateCommand();
                cmd.CommandText =
                    "INSERT INTO news_classify_batches (applied, pending_after, created_at) VALUES ($a, $p, $at)";
                cmd.Parameters.AddWithValue("$a", applied);
                cmd.Parameters.AddWithValue("$p", pendingAfter);
                cmd.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O"));
                cmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "metrics: RecordNewsClassify failed");
        }
    }

    public void RecordRateLimit(string reason)
    {
        try
        {
            lock (_gate)
            {
                using var c = Open();
                c.Open();
                using var cmd = c.CreateCommand();
                cmd.CommandText =
                    "INSERT INTO rate_limit_hits (reason, created_at) VALUES ($r, $at)";
                cmd.Parameters.AddWithValue("$r", reason);
                cmd.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O"));
                cmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "metrics: RecordRateLimit failed");
        }
    }

    public object Snapshot()
    {
        lock (_gate)
        {
            using var c = Open();
            c.Open();

            long runs = Scalar(c, "SELECT COUNT(*) FROM insight_runs");
            long ok = Scalar(c, "SELECT COUNT(*) FROM insight_runs WHERE status='ok'");
            long err = Scalar(c, "SELECT COUNT(*) FROM insight_runs WHERE status='error'");
            long clarify = Scalar(c, "SELECT COUNT(*) FROM insight_runs WHERE is_clarify=1");
            long withSrc = Scalar(c, "SELECT COUNT(*) FROM insight_runs WHERE status='ok' AND sources_count>0");
            long scOk = Scalar(c, "SELECT COUNT(*) FROM insight_runs WHERE self_check='ok'");
            long scFail = Scalar(c, "SELECT COUNT(*) FROM insight_runs WHERE self_check='fail'");
            long scRetry = Scalar(c, "SELECT COUNT(*) FROM insight_runs WHERE self_check_retried=1");
            long votesUp = Scalar(c, "SELECT COUNT(*) FROM insight_votes WHERE vote='up'");
            long votesDown = Scalar(c, "SELECT COUNT(*) FROM insight_votes WHERE vote='down'");
            long rateHits = Scalar(c, "SELECT COUNT(*) FROM rate_limit_hits");
            long newsApplied = Scalar(c, "SELECT COALESCE(SUM(applied),0) FROM news_classify_batches");

            double avgMs = 0, p95Ms = 0;
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "SELECT AVG(duration_ms) FROM insight_runs WHERE status='ok'";
                var v = cmd.ExecuteScalar();
                if (v != null && v != DBNull.Value)
                    avgMs = Convert.ToDouble(v);
            }

            var durations = new List<long>();
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "SELECT duration_ms FROM insight_runs WHERE status='ok' ORDER BY duration_ms";
                using var r = cmd.ExecuteReader();
                while (r.Read()) durations.Add(r.GetInt64(0));
            }

            if (durations.Count > 0)
            {
                var idx = Math.Clamp((int)Math.Ceiling(durations.Count * 0.95) - 1, 0, durations.Count - 1);
                p95Ms = durations[idx];
            }

            double Pct(long part, long whole) => whole == 0 ? 0 : Math.Round(100.0 * part / whole, 1);

            return new
            {
                storage = "sqlite",
                dbPath = Path.GetFullPath(new SqliteConnectionStringBuilder(_cs).DataSource),
                runs = new
                {
                    total = runs,
                    ok,
                    error = err,
                    clarify,
                    okRatePct = Pct(ok, runs),
                },
                quality = new
                {
                    withSources = withSrc,
                    withSourcesPct = Pct(withSrc, ok),
                    selfCheckOk = scOk,
                    selfCheckFail = scFail,
                    selfCheckOkPct = Pct(scOk, scOk + scFail),
                    selfCheckRetried = scRetry,
                },
                latencyMs = new
                {
                    avg = Math.Round(avgMs, 0),
                    p95 = p95Ms,
                },
                votes = new { up = votesUp, down = votesDown },
                newsLlmClassified = newsApplied,
                rateLimitHits = rateHits,
                utc = DateTimeOffset.UtcNow,
            };
        }
    }

    static long Scalar(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        var v = cmd.ExecuteScalar();
        return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
    }

    SqliteConnection Open() => new(_cs);
}
