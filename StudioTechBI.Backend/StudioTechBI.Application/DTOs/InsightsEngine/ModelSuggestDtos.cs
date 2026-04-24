using System.Text.Json;

namespace StudioTechBI.Application.DTOs.InsightsEngine;

/// <summary>Request to InsightsEngine.Api to propose dashboard/data models from a small sample.</summary>
public sealed class ModelSuggestRequest
{
    public string ClientId { get; set; } = "";
    public int MaxRows { get; set; } = 100;
    public string? UserPrompt { get; set; }
    public JsonElement Sample { get; set; }
}

public sealed class ModelSuggestResponse
{
    public string? Provider { get; set; }
    public string? AnalysisSummary { get; set; }
    public List<DetectedColumn> DetectedSchema { get; set; } = new();
    public List<ProposedModel> ProposedModels { get; set; } = new();
}

public sealed class DetectedColumn
{
    public string? Name { get; set; }
    public string? InferredType { get; set; }
}

public sealed class ProposedModel
{
    public string? ModelId { get; set; }
    public string? Title { get; set; }
    public string? Rationale { get; set; }
    public List<string> SuggestedTemplates { get; set; } = new();
    public List<string> ReportIdeas { get; set; } = new();
    public double Confidence { get; set; }
}

