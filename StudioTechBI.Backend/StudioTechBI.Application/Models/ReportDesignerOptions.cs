namespace StudioTechBI.Application.Models;

public class ReportDesignerOptions
{
    public const string SectionName = "ReportDesigner";

    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Budgets the entire outbound call to stbi_transformers, including the schema-match and
    /// model-generation endpoints, which call out to an LLM downstream. Must stay comfortably
    /// above that LLM call's own timeout (raised to 5 minutes / 300s alongside the MaxTokens
    /// increase to 32000 — see stbi_transformers' Program.cs AnthropicClient HttpClient timeout)
    /// or a legitimately-slow-but-successful call gets killed here before the downstream side
    /// would have given up on it.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 330;
}
