using System.Text.Json.Serialization;

namespace StudioTechBI.Application.DTOs.InsightsEngine;

public sealed class ReportPageInsightsRequest
{
    public string ReportType { get; set; } = "monthly";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Period { get; set; }

    public string ActivePageName { get; set; } = string.Empty;

    public List<string> VisualTitles { get; set; } = new();

    public List<string> Prompts { get; set; } = new();
}

public sealed class ReportPageInsightsResponse
{
    public string Provider { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Summary { get; set; }

    public List<string> Insights { get; set; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? FollowUps { get; set; }
}

