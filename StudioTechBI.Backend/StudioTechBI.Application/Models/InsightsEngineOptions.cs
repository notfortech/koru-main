namespace StudioTechBI.Application.Models;

/// <summary>Configuration for the external InsightsEngine transformations suggestion API.</summary>
public sealed class InsightsEngineOptions
{
    public const string SectionName = "InsightsEngine";

    public string BaseUrl { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
}

