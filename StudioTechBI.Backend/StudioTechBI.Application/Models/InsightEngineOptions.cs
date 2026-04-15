namespace StudioTechBI.Application.Models;

public class InsightEngineOptions
{
    public const string SectionName = "InsightEngine";

    /// <summary>When false, model endpoints short-circuit without calling the external API or writing insight tables.</summary>
    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Optional API key sent as Bearer token if non-empty.</summary>
    public string? ApiKey { get; set; }

    public int TimeoutSeconds { get; set; } = 120;
}
