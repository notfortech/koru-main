namespace StudioTechBI.Application.Models;

/// <summary>Config for the typed client to DashboardAgents.ReportValidationApi (the Playwright
/// rendering-health service, a separate deployable in stbi_transformers — same shape as
/// ReportGeneratorOptions for its sibling service.</summary>
public class ReportValidationOptions
{
    public const string SectionName = "ReportValidation";

    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 180;

    /// <summary>The frontend origin Playwright should navigate to (e.g. https://app.studiotechbi.com).
    /// Passed through to the rendering-health call so it's environment-driven, not hardcoded.</summary>
    public string AppBaseUrl { get; set; } = string.Empty;
}
