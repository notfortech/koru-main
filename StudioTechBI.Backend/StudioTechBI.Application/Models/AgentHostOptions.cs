using System.ComponentModel.DataAnnotations;

namespace StudioTechBI.Application.Models;

public class AgentHostOptions
{
    public const string SectionName = "AgentHost";

    [Required(AllowEmptyStrings = false,
        ErrorMessage = "AgentHost:BaseUrl is required. Set the AGENT_HOST_BASE_URL environment variable.")]
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 300;
    public int RetryCount { get; set; } = 3;
}
