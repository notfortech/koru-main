namespace StudioTechBI.Application.Models;

public class AgentHostOptions
{
    public const string SectionName = "AgentHost";

    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// AgentHost's X-Admin-Key, required only for calls into its /api/admin/* surface (e.g.
    /// GrantCreditsAsync) — separate from ApiKey above, which gates AgentHost's regular
    /// endpoints. Matches AgentHost's own AdminSettings.AdminApiKey.
    /// </summary>
    public string AdminApiKey { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 300;
    public int RetryCount { get; set; } = 3;

    public bool EnableCircuitBreaker { get; set; } = true;
    public int CircuitFailureThreshold { get; set; } = 5;
    public int CircuitBreakDurationSeconds { get; set; } = 30;
    public string HealthCheckPath { get; set; } = "api/health";
}
