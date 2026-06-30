namespace StudioTechBI.Application.Models;

public sealed class StbAgentHostOptions
{
    public const string SectionName = "StbAgentHost";

    public string BaseUrl { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public int TimeoutSeconds { get; set; } = 120;
}
