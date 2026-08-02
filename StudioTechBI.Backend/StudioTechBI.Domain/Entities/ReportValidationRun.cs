namespace StudioTechBI.Domain.Entities;

/// <summary>
/// Tracks one on-demand "Validate Report" run (Pending → Processing → Completed/Failed) against a
/// report the user already generated. ReportSnapshotJson holds the exact GeneratedReportDto the
/// user submitted, so Data Sanity checks validate what's actually on their screen rather than a
/// fresh recompute; SourceFileScratchBlobPath is scratch storage for this one run only (deleted on
/// completion), never a durable "report by ID" — this pipeline has no report persistence otherwise.
/// </summary>
public class ReportValidationRun : BaseEntity
{
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;
    public Guid RequestedByUserId { get; set; }

    /// <summary>Pending | Processing | Completed | Failed</summary>
    public string Status { get; set; } = ReportValidationStatuses.Pending;

    /// <summary>Pass | Warning | Fail | Error — worst-of across every check in this run.</summary>
    public string? OverallResult { get; set; }

    public string? TemplateId { get; set; }
    public string? TemplateName { get; set; }

    /// <summary>JSON-serialised {column: value} — same shape as GeneratedReportDto.AppliedFilters.</summary>
    public string? FiltersJson { get; set; }

    /// <summary>The exact GeneratedReportDto submitted for validation, verbatim JSON.</summary>
    public string ReportSnapshotJson { get; set; } = string.Empty;

    /// <summary>Scratch blob path to the uploaded source file for this run only — deleted after
    /// completion/TTL. Never a durable, revisitable artifact.</summary>
    public string? SourceFileScratchBlobPath { get; set; }

    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }

    public ICollection<ReportValidationCheck> Checks { get; set; } = new List<ReportValidationCheck>();
}

public static class ReportValidationStatuses
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}

public static class ReportValidationResults
{
    public const string Pass = "Pass";
    public const string Warning = "Warning";
    public const string Fail = "Fail";
    public const string Error = "Error";
}
