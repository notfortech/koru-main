namespace StudioTechBI.Domain.Entities;

/// <summary>
/// Tracks one async AI-assisted Report Generator "Data Model" generation job
/// (Pending -> Processing -> Completed/Failed). Lets the client submit the schema, navigate away,
/// and come back later instead of holding a browser connection open for the multi-minute LLM call
/// -- mirrors BlueprintGeneration's shape and lifecycle exactly. RequestPayloadJson stores the
/// original GenerateReportModelRequest (schema included) so the background worker is self-contained
/// AND so the frontend can reconstruct its wizard state (the extracted schema) when resuming from a
/// notification click, without needing to persist that separately client-side.
/// </summary>
public class ReportModelGeneration : BaseEntity
{
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;

    /// <summary>Correlation ID returned to the caller immediately on 202 Accepted.</summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>Pending | Processing | Completed | Failed</summary>
    public string Status { get; set; } = ReportModelGenerationStatuses.Pending;

    /// <summary>JSON-serialised GenerateReportModelRequest so the background worker is
    /// self-contained and the frontend can resume its wizard state from the schema alone.</summary>
    public string RequestPayloadJson { get; set; } = string.Empty;

    /// <summary>JSON-serialised GenerateReportModelResponse, set once the job completes successfully.</summary>
    public string? ResponseJson { get; set; }

    public string? ErrorMessage { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public static class ReportModelGenerationStatuses
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}
