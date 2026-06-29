using System.ComponentModel.DataAnnotations;

namespace StudioTechBI.Application.DTOs.Blueprints;

/// <summary>
/// Request posted by the React portal to kick off a dashboard blueprint generation.
/// Koru passes this to STBI-AgentHost without any knowledge of AI internals.
/// </summary>
public class GenerateBlueprintRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid ClientId { get; set; }

    public string? ProjectId { get; set; }

    public string? BusinessRequirements { get; set; }

    [Required, MaxLength(200)]
    public string Industry { get; set; } = string.Empty;

    public string? KnowledgePack { get; set; }

    public string? ExistingSchema { get; set; }

    public string? SampleData { get; set; }

    public List<string>? DataConnections { get; set; }

    public List<string>? ExistingReports { get; set; }

    public string OutputLanguage { get; set; } = "en";

    public bool GeneratePdf { get; set; } = true;
    public bool GenerateJson { get; set; } = true;
}
