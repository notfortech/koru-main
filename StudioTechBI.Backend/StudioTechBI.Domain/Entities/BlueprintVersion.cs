namespace StudioTechBI.Domain.Entities;

/// <summary>
/// Immutable snapshot of one generation result.
/// Blob paths point to either Azure Blob Storage or local filesystem — never binary data in SQL.
/// </summary>
public class BlueprintVersion : BaseEntity
{
    public Guid BlueprintId { get; set; }
    public Blueprint Blueprint { get; set; } = null!;

    public int VersionNumber { get; set; }
    public string? PromptVersion { get; set; }
    public double Confidence { get; set; }

    /// <summary>Relative blob path for the Analytics Deployment Contract JSON.</summary>
    public string? JsonBlobPath { get; set; }

    /// <summary>Relative blob path for the Blueprint PDF.</summary>
    public string? PdfBlobPath { get; set; }

    public DateTime GeneratedDate { get; set; }
    public long ExecutionTimeMs { get; set; }

    /// <summary>Only one version per Blueprint is active at a time.</summary>
    public bool IsActive { get; set; }

    /// <summary>Analytics contract JSON stored inline for fast retrieval without blob I/O.</summary>
    public string? GeneratedJsonContent { get; set; }
}
