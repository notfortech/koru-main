namespace StudioTechBI.Application.Models;

public class BindDeployOptions
{
    public const string SectionName = "BindDeploy";

    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Sent as X-Service-Api-Key — must match stbi-bind-deploy's own INBOUND_SERVICE_API_KEY.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 60;
}
