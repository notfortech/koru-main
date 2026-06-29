namespace StudioTechBI.Domain.Entities;

/// <summary>
/// Top-level aggregate representing a dashboard blueprint for a tenant/client/project.
/// One Blueprint per project; each regeneration produces a new BlueprintVersion.
/// </summary>
public class Blueprint : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid ClientId { get; set; }
    public string? ProjectId { get; set; }
    public string Industry { get; set; } = string.Empty;
    public string? KnowledgePack { get; set; }

    /// <summary>Active or Archived.</summary>
    public string Status { get; set; } = BlueprintStatuses.Active;

    /// <summary>Monotonically increasing counter — used as the version number on each BlueprintVersion.</summary>
    public int VersionCount { get; set; }

    public ICollection<BlueprintVersion> Versions { get; set; } = new List<BlueprintVersion>();
    public ICollection<BlueprintGeneration> Generations { get; set; } = new List<BlueprintGeneration>();
}
