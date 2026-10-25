using System.Text.Json;

namespace Volanchiki.Api.Agent;

public interface IAgentTool
{
    string Name { get; }
    string Description { get; }
    Task<string> ExecuteAsync(JsonElement args, AgentSession session, CancellationToken ct);
}

public class AgentSession
{
    public string RunId { get; } = Guid.NewGuid().ToString("N");
    public string CompanyId { get; set; } = "";
    public string? NewsHint { get; set; }
    public Dictionary<string, string> Scratch { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<Domain.AgentTraceStep> Trace { get; } = new();

    public void Note(string action, string? tool = null, string? detail = null)
    {
        if (detail != null && detail.Length > 1200)
            detail = detail.Substring(0, 1200) + "…";

        Trace.Add(new Domain.AgentTraceStep
        {
            Step = Trace.Count + 1,
            Action = action,
            Tool = tool,
            Detail = detail,
        });
    }
}
