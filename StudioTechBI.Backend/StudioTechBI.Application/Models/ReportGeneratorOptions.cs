namespace StudioTechBI.Application.Models;

/// <summary>
/// Config for DashboardAgents.ReportAgent.Api — the deterministic, no-AI
/// report engine (separate Azure Container App from ReportDesigner/AgentHost).
/// </summary>
public class ReportGeneratorOptions
{
    public const string SectionName = "ReportGenerator";

    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 120;
}
