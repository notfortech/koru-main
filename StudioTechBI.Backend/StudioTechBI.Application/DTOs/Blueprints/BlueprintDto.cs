namespace StudioTechBI.Application.DTOs.Blueprints;

public class BlueprintDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ClientId { get; set; }
    public string? ProjectId { get; set; }
    public string Industry { get; set; } = string.Empty;
    public string? KnowledgePack { get; set; }
    public string Status { get; set; } = string.Empty;
    public int VersionCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }

    public BlueprintVersionDto? ActiveVersion { get; set; }
}

public class BlueprintVersionDto
{
    public Guid Id { get; set; }
    public int VersionNumber { get; set; }
    public string? PromptVersion { get; set; }
    public double Confidence { get; set; }
    public DateTime GeneratedDate { get; set; }
    public long ExecutionTimeMs { get; set; }
    public bool HasJson { get; set; }
    public bool HasPdf { get; set; }
    public string? GeneratedJsonContent { get; set; }
}
