using System.ComponentModel.DataAnnotations;

namespace StudioTechBI.Application.DTOs.Blueprints;

/// <summary>
/// Request posted by the React portal to kick off a dashboard blueprint generation.
/// Koru passes this to STBI-AgentHost without any knowledge of AI internals.
/// </summary>
public class GenerateBlueprintRequest
{
    /// <summary>
    /// Optional — resolved server-side from the JWT client_code claim when absent.
    /// Frontend only needs to supply this if overriding the JWT-bound client.
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Optional — resolved server-side from the JWT client_code claim when absent.
    /// Frontend only needs to supply this if overriding the JWT-bound client.
    /// </summary>
    public Guid? ClientId { get; set; }

    /// <summary>Client code (e.g. AU-004). Used server-side to look up ClientId when ClientId is not supplied.</summary>
    public string? ClientCode { get; set; }

    public string? ProjectId { get; set; }

    [Required, MaxLength(200)]
    public string Industry { get; set; } = string.Empty;

    public string? KnowledgePack { get; set; }

    [Required]
    public string BusinessCapability { get; set; } = string.Empty;

    [Required]
    public string BusinessGoal { get; set; } = string.Empty;

    public string? BusinessRequirements { get; set; }

    public List<SourceSystemDto>? SourceSystems { get; set; }

    public List<DatasetMetadataDto>? DatasetMetadata { get; set; }

    public string? PreferredProvider { get; set; }

    public string? PreferredModel { get; set; }

    public double? Temperature { get; set; }

    public int? MaxTokens { get; set; }

    public string? OutputFormat { get; set; }
}

public class SourceSystemDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Type { get; set; }

    public string? Description { get; set; }

    public List<string>? Tables { get; set; }
}

public class DatasetMetadataDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<ColumnMetadataDto>? Columns { get; set; }
}

public class ColumnMetadataDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? DataType { get; set; }

    public string? Description { get; set; }

    public bool? IsPrimaryKey { get; set; }

    public bool? IsNullable { get; set; }
}
