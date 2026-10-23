namespace Volanchiki.Api.Domain;

public class InsightCard
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string CompanyId { get; set; } = "";
    public string Title { get; set; } = "";
    public string WhatHappened { get; set; } = "";
    public string WhyMattersForCompany { get; set; } = "";
    public string BankView { get; set; } = "";
    public string ModelView { get; set; } = "";
    public List<string> Risks { get; set; } = new();
    public List<string> Opportunities { get; set; } = new();
    public List<string> SuggestedActions { get; set; } = new();
    public List<InsightSource> Sources { get; set; } = new();
    public string Relevance { get; set; } = "medium";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Disclaimer { get; set; } =
        "Материал носит информационный характер и не является индивидуальной инвестиционной рекомендацией.";
    public string Cta { get; set; } = "Связаться с менеджером";
}

public class InsightSource
{
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Url { get; set; }
    public string? Excerpt { get; set; }
}

public class AgentTraceStep
{
    public int Step { get; set; }
    public string Action { get; set; } = "";
    public string? Tool { get; set; }
    public string? Detail { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public class AgentRunResult
{
    public string RunId { get; set; } = Guid.NewGuid().ToString("N");
    public InsightCard? Insight { get; set; }
    public List<AgentTraceStep> Trace { get; set; } = new();
    public string Status { get; set; } = "ok";
    public string? Error { get; set; }
}
