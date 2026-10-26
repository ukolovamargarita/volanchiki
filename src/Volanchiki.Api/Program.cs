using Volanchiki.Api.Agent;
using Volanchiki.Api.Agent.Tools;
using Volanchiki.Api.Data;
using Volanchiki.Api.Llm;
using Volanchiki.Api.News;
using Volanchiki.Api.Options;

// .env из корня репо или рядом с api
LoadEnv();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.Configure<DataPathsOptions>(o =>
{
    o.CompanyDir = Environment.GetEnvironmentVariable("DATA_COMPANY_DIR")
                   ?? builder.Configuration["Data:CompanyDir"]
                   ?? o.CompanyDir;
    o.AlfaDir = Environment.GetEnvironmentVariable("DATA_ALFA_DIR")
                ?? builder.Configuration["Data:AlfaDir"]
                ?? o.AlfaDir;
});

builder.Services.Configure<NewsOptions>(builder.Configuration.GetSection(NewsOptions.Section));
if (int.TryParse(Environment.GetEnvironmentVariable("NEWS_POLL_SECONDS"), out var pollSec) && pollSec > 0)
    builder.Services.PostConfigure<NewsOptions>(o => o.PollIntervalSeconds = pollSec);

builder.Services.AddSingleton<CompanyStore>();
builder.Services.AddSingleton<CsvCompanyLoader>();
builder.Services.AddSingleton<AlfaAnalyticsStore>();
builder.Services.AddSingleton<AlfaTxtLoader>();
builder.Services.AddSingleton<NewsStore>();
builder.Services.AddSingleton<InsightRunStore>();
builder.Services.AddSingleton<InsightRunGuard>();
builder.Services.AddSingleton<MetricsDb>();
builder.Services.AddSingleton<RssNewsParser>();
builder.Services.AddSingleton<NewsLlmClassifier>();
builder.Services.AddHostedService<DataWarmupHostedService>();
builder.Services.AddHostedService<RssNewsIngestionHostedService>();

builder.Services.AddHttpClient("rss", c =>
{
    c.Timeout = TimeSpan.FromSeconds(25);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("VolanchikiNewsBot/1.0 (+hackathon; RSS reader)");
    c.DefaultRequestHeaders.Accept.ParseAdd("application/rss+xml, application/atom+xml, application/xml, text/xml, */*");
});

builder.Services.AddLlm(builder.Configuration);

builder.Services.AddScoped<IAgentTool, GetCompanyProfileTool>();
builder.Services.AddScoped<IAgentTool, SearchNewsTool>();
builder.Services.AddScoped<IAgentTool, SearchAlfaAnalyticsTool>();
builder.Services.AddScoped<IAgentTool, DraftInsightTool>();
builder.Services.AddScoped<AgentOrchestrator>();

var corsOrigins = (Environment.GetEnvironmentVariable("CORS_ORIGINS") ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
if (corsOrigins.Length == 0)
{
    corsOrigins =
    [
        "http://localhost:5173",
        "http://localhost:5080",
        "http://localhost:8080",
        "http://127.0.0.1:5173",
        "http://127.0.0.1:5080",
        "http://127.0.0.1:8080",
        "https://alfa.dev.news.myrelo.ru",
    ];
}

builder.Services.AddCors(c =>
    c.AddDefaultPolicy(p => p
        .WithOrigins(corsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

app.UseCors();

// Если рядом лежит собранный фронтенд (wwwroot, как в Docker-образе), отдаём его же
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();

static void LoadEnv()
{
    foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        DirectoryInfo? dir = new(start);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, ".env");
            if (File.Exists(candidate))
            {
                DotNetEnv.Env.Load(candidate);
                Console.WriteLine($"env: {candidate}");
                var key = Environment.GetEnvironmentVariable("LLM_API_KEY");
                Console.WriteLine(string.IsNullOrWhiteSpace(key)
                    ? "LLM_API_KEY: empty"
                    : $"LLM_API_KEY: loaded ({key.Length} chars)");
                return;
            }

            dir = dir.Parent;
        }
    }

    Console.WriteLine("env: .env not found (walking up from cwd/bin)");
}
