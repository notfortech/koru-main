using System.Text.Json;

namespace StudioTechBI.Application.DTOs.InsightsEngine;

public sealed class TransformSuggestRequest
{
    public string ClientId { get; set; } = "";
    public int MaxRows { get; set; } = 100;
    public string? UserPrompt { get; set; }
    public JsonElement Sample { get; set; }
}

public sealed class TransformSuggestResponse
{
    public string? Provider { get; set; }
    public string? Summary { get; set; }
    public List<ColumnProfile> Columns { get; set; } = new();
    public List<DataIssue> Issues { get; set; } = new();
    public List<TransformStep> Steps { get; set; } = new();
}

public sealed class ColumnProfile
{
    public string? Name { get; set; }
    public string? InferredType { get; set; }
    public double? MissingRatio { get; set; }
    public double? DistinctRatio { get; set; }
}

public sealed class DataIssue
{
    public string? Code { get; set; }
    public string? Severity { get; set; }
    public string? Message { get; set; }
    public string? Column { get; set; }
}

public sealed class TransformStep
{
    public int Order { get; set; }
    public string? Action { get; set; }
    public Dictionary<string, JsonElement>? Parameters { get; set; }
}

