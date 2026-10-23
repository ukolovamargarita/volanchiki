using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Volanchiki.Api.Llm;

public class LlmClient
{
    readonly HttpClient _http;
    readonly LlmOptions _opt;
    readonly ILogger<LlmClient> _log;

    public LlmClient(HttpClient http, IOptions<LlmOptions> opt, ILogger<LlmClient> log)
    {
        _http = http;
        _opt = opt.Value;
        _log = log;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_opt.ApiKey);

    public Task<string> ChatAsync(
        string system,
        string user,
        string? model = null,
        float temperature = 0.2f,
        CancellationToken ct = default)
        => ChatAsync(
            [
                new LlmMessage("system", system),
                new LlmMessage("user", user),
            ],
            model,
            temperature,
            ct);

    public async Task<string> ChatAsync(
        IReadOnlyList<LlmMessage> messages,
        string? model = null,
        float temperature = 0.2f,
        CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("LLM_API_KEY не задан");

        var body = new
        {
            model = model ?? _opt.ModelDefault,
            temperature,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opt.ApiKey);

        using var resp = await _http.SendAsync(req, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            _log.LogWarning("LLM {Status}: {Body}", (int)resp.StatusCode, Trunc(raw, 500));
            throw new HttpRequestException($"LLM gateway {(int)resp.StatusCode}: {Trunc(raw, 300)}");
        }

        using var doc = JsonDocument.Parse(raw);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return content?.Trim() ?? "";
    }

    public string PickModel(LlmRole role) => role switch
    {
        LlmRole.Light => _opt.ModelLight,
        LlmRole.Reasoning => _opt.ModelReasoning,
        LlmRole.Research => _opt.ModelResearch,
        _ => _opt.ModelDefault,
    };

    private static string Trunc(string s, int n) =>
        s.Length <= n ? s : s[..n] + "…";
}

public enum LlmRole
{
    Default,
    Light,
    Reasoning,
    Research,
}

public sealed record LlmMessage(string Role, string Content);

public static class LlmServiceCollectionExtensions
{
    public static IServiceCollection AddLlm(this IServiceCollection services, IConfiguration cfg)
    {
        services.Configure<LlmOptions>(o =>
        {
            o.ApiKey = Env("LLM_API_KEY") ?? cfg["LLM:ApiKey"] ?? "";
            o.BaseUrl = Env("LLM_BASE_URL") ?? cfg["LLM:BaseUrl"] ?? o.BaseUrl;
            o.ModelLight = Env("LLM_MODEL_LIGHT") ?? o.ModelLight;
            o.ModelReasoning = Env("LLM_MODEL_REASONING") ?? o.ModelReasoning;
            o.ModelDefault = Env("LLM_MODEL_DEFAULT") ?? o.ModelDefault;
            o.ModelResearch = Env("LLM_MODEL_RESEARCH") ?? o.ModelResearch;
        });

        var useSystemProxy = string.Equals(
            Env("LLM_USE_SYSTEM_PROXY"), "true", StringComparison.OrdinalIgnoreCase);

        services.AddHttpClient<LlmClient>((sp, http) =>
            {
                var opt = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
                var baseUrl = opt.BaseUrl.TrimEnd('/') + "/";
                http.BaseAddress = new Uri(baseUrl);
                http.Timeout = TimeSpan.FromMinutes(2);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                UseProxy = useSystemProxy,
                Proxy = useSystemProxy ? HttpClient.DefaultProxy : null,
            });

        return services;
    }

    private static string? Env(string key)
    {
        var v = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }
}
