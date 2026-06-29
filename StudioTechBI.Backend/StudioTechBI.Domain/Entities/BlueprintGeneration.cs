namespace StudioTechBI.Domain.Entities;

/// <summary>
/// Tracks one async generation job (Pending → Processing → Completed/Failed).
/// The serialised request payload is stored here so the background worker can
/// reconstruct the AgentHost call without re-reading from the HTTP context.
/// </summary>
public class BlueprintGeneration : BaseEntity
{
    public Guid BlueprintId { get; set; }
    public Blueprint Blueprint { get; set; } = null!;

    /// <summary>Correlation ID returned to the caller immediately on 202 Accepted.</summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>Pending | Processing | Completed | Failed</summary>
    public string Status { get; set; } = BlueprintStatuses.Pending;

    /// <summary>JSON-serialised GenerateBlueprintRequest so the background worker is self-contained.</summary>
    public string? RequestPayloadJson { get; set; }

    public bool GeneratePdf { get; set; }
    public bool GenerateJson { get; set; }

    public double? ConfidenceScore { get; set; }

    /// <summary>Semicolon-separated warning messages from AgentHost.</summary>
    public string? Warnings { get; set; }

    public string? ErrorMessage { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>Set once the job completes successfully.</summary>
    public Guid? BlueprintVersionId { get; set; }
    public BlueprintVersion? BlueprintVersion { get; set; }
}
