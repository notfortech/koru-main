namespace StudioTechBI.Application.DTOs.Blueprint;

/// <summary>Request body sent to POST /api/blueprints/generate on STBI AgentHost.</summary>
public sealed class StbAgentHostRequestDto
{
    public string BusinessRequirement { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;

    /// <summary>Optional JSON string. Null when not provided.</summary>
    public string? ExistingSchema { get; set; }
}
