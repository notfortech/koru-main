namespace StudioTechBI.Application.Models;

public class AgentHostOptions
{
    public const string SectionName = "AgentHost";

    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 300;
    public int RetryCount { get; set; } = 3;
}
