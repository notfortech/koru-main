namespace StudioTechBI.Domain.Entities;

/// <summary>
/// Tracks one async AI-assisted Report Generator schema-model library match job
/// (Pending -> Processing -> Completed/Failed). Lets the client submit the schema, navigate away,
/// and come back later instead of holding a browser connection open for up to ~330s (the
/// deterministic path is fast, but an AI-escalated match can take the full outbound AI budget) --
/// mirrors ReportModelGeneration's shape and lifecycle exactly. RequestPayloadJson stores the
/// original ReportMatchRequest (schema included) so the background worker is self-contained AND
/// so the frontend can reconstruct its wizard state when resuming from a notification click.
/// </summary>
public class SchemaModelMatch : BaseEntity
{
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;

    /// <summary>Correlation ID returned to the caller immediately on 202 Accepted.</summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>Pending | Processing | Completed | Failed</summary>
    public string Status { get; set; } = SchemaModelMatchStatuses.Pending;

    /// <summary>JSON-serialised ReportMatchRequest so the background worker is self-contained and
    /// the frontend can resume its wizard state from the schema alone.</summary>
    public string RequestPayloadJson { get; set; } = string.Empty;

    /// <summary>JSON-serialised ReportMatchResultDto, set once the job completes successfully.</summary>
    public string? ResponseJson { get; set; }

    public string? ErrorMessage { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public static class SchemaModelMatchStatuses
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}
