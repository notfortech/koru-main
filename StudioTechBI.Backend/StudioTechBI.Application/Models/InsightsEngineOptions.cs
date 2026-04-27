namespace StudioTechBI.Application.Models;

/// <summary>Configuration for the external InsightsEngine transformations suggestion API.</summary>
public sealed class InsightsEngineOptions
{
    public const string SectionName = "InsightsEngine";

    public string BaseUrl { get; set; } = string.Empty;
    public string? ApiKey { get; set; }

    /// <summary>HTTP timeout when calling InsightsEngine.Api.</summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// When true, copilot/routes that support it may call the external InsightsEngine.Api
    /// (transformations, models, plans, template mapping). When false, the API uses only
    /// in-app template catalog + blob sampling (no outbound AI HTTP calls for these routes).
    /// </summary>
    public bool ExternalCopilotAiEnabled { get; set; }

    /// <summary>
    /// If the best catalog match score (required/optional column overlap) is <b>at or above</b> this value,
    /// external copilot AI calls are skipped (schema already fits an existing template well enough).
    /// Range 0..1. Ignored when <see cref="ExternalCopilotAiEnabled"/> is false.
    /// </summary>
    public double MinTemplateMatchScoreToSkipExternalAi { get; set; } = 0.85;
}

