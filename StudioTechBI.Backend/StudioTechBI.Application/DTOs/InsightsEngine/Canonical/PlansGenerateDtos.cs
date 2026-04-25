using System.Text.Json;

namespace StudioTechBI.Application.DTOs.InsightsEngine.Canonical;

/// <summary>Matches InsightsEngine.Api POST /api/ai/plans/generate input shape (InsightAiSampleRequest).</summary>
public sealed class PlansGenerateRequest
{
    public string ClientId { get; set; } = string.Empty;
    public string Mode { get; set; } = "PredictScenarios"; // matches JsonStringEnumConverter on external API
    public string? UserScenario { get; set; } // repurposed as user prompt/goal
    public JsonElement Sample { get; set; }
}

/// <summary>Matches InsightsEngine.Api /api/ai/plans/generate output shape.</summary>
public sealed class PlansGenerateResponse
{
    public string Provider { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<DashboardTemplatePlan> Plans { get; set; } = new();
    public CanonicalValidationResult? Validation { get; set; }
}

