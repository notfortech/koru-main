namespace StudioTechBI.Application.DTOs.InsightsEngine.Canonical;

public sealed class DashboardTemplatePlan
{
    public SemanticModel Model { get; set; } = new();
    public DashboardDefinition Dashboard { get; set; } = new();
    public double Confidence { get; set; }
    public string Rationale { get; set; } = string.Empty;
}

