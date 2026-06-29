namespace StudioTechBI.Application.DTOs.Blueprints;

/// <summary>
/// Internal DTO sent to STBI-AgentHost POST /api/blueprints/generate.
/// Strips Koru-specific fields (TenantId, ClientId, ProjectId) and maps
/// the caller's request to the exact AgentHost contract.
/// </summary>
public class AgentHostBlueprintRequest
{
    public Guid RequestId { get; set; }

    public string Industry { get; set; } = string.Empty;

    public string? KnowledgePack { get; set; }

    public string BusinessCapability { get; set; } = string.Empty;

    public string BusinessGoal { get; set; } = string.Empty;

    public string? BusinessRequirements { get; set; }

    public List<SourceSystemDto>? SourceSystems { get; set; }

    public List<DatasetMetadataDto>? DatasetMetadata { get; set; }

    public string? PreferredProvider { get; set; }

    public string? PreferredModel { get; set; }

    public double? Temperature { get; set; }

    public int? MaxTokens { get; set; }

    public string? OutputFormat { get; set; }

    public static AgentHostBlueprintRequest From(GenerateBlueprintRequest req, Guid requestId) => new()
    {
        RequestId = requestId,
        Industry = req.Industry,
        KnowledgePack = req.KnowledgePack,
        BusinessCapability = req.BusinessCapability,
        BusinessGoal = req.BusinessGoal,
        BusinessRequirements = req.BusinessRequirements,
        SourceSystems = req.SourceSystems,
        DatasetMetadata = req.DatasetMetadata,
        PreferredProvider = req.PreferredProvider,
        PreferredModel = req.PreferredModel,
        Temperature = req.Temperature,
        MaxTokens = req.MaxTokens,
        OutputFormat = req.OutputFormat,
    };
}
