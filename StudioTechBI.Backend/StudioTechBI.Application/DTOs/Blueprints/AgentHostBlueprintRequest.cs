namespace StudioTechBI.Application.DTOs.Blueprints;

/// <summary>
/// Internal DTO sent to STBI-AgentHost POST /api/blueprints/generate.
/// Maps the caller's request to AgentHost's actual public contract: industry,
/// businessCapability, businessGoal, and an optional existingSchema — nothing
/// else is accepted on the wire. Koru-specific fields (TenantId, ClientId,
/// ProjectId) are stripped; KnowledgePack/SourceSystems/DatasetMetadata/
/// PreferredProvider etc. aren't part of this contract and are not sent.
/// </summary>
public class AgentHostBlueprintRequest
{
    public string Industry { get; set; } = string.Empty;

    public string BusinessCapability { get; set; } = string.Empty;

    public string BusinessGoal { get; set; } = string.Empty;

    public string? ExistingSchema { get; set; }

    public static AgentHostBlueprintRequest From(GenerateBlueprintRequest req) => new()
    {
        Industry = req.Industry,
        BusinessCapability = req.BusinessCapability,
        BusinessGoal = BuildBusinessGoal(req),
    };

    private static string BuildBusinessGoal(GenerateBlueprintRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.BusinessRequirements))
            return req.BusinessGoal;

        return $"""
                {req.BusinessGoal}

                Additional requirements: {req.BusinessRequirements}
                """;
    }
}
