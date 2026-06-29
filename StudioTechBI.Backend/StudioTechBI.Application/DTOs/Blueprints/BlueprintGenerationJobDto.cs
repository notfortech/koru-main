namespace StudioTechBI.Application.DTOs.Blueprints;

/// <summary>
/// Returned on 202 Accepted and from the status-polling endpoint.
/// </summary>
public class BlueprintGenerationJobDto
{
    public Guid GenerationId { get; set; }
    public Guid BlueprintId { get; set; }

    /// <summary>Correlation ID that matches the RequestId in the AgentHost response.</summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>Pending | Processing | Completed | Failed</summary>
    public string Status { get; set; } = string.Empty;

    public double? ConfidenceScore { get; set; }
    public List<string>? Warnings { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>Populated once the job completes successfully.</summary>
    public Guid? BlueprintVersionId { get; set; }
}
