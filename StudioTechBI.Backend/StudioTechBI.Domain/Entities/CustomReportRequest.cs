namespace StudioTechBI.Domain.Entities;

/// <summary>
/// A client's request for a bespoke, analyst-built Power BI report — filed when nothing in the
/// self-serve HTML/PBIP template catalogs matches their data well enough. Deliberately a new
/// entity rather than reusing ReportingReportRequest: that entity requires a non-nullable
/// TemplateId into the PBIP catalog, but a "custom" request exists precisely because nothing in
/// that catalog matched. SchemaSnapshotJson is the first durable, admin-retrievable home for a
/// client's extracted schema — today it's only ever computed in-memory and returned to the client.
/// </summary>
public class CustomReportRequest : BaseEntity
{
    public Guid ClientId { get; set; }
    public string Status { get; set; } = CustomReportRequestStatuses.Pending;
    public string? RequestedByEmail { get; set; }
    public string? Notes { get; set; }

    /// <summary>Why this ticket exists -- see CustomReportRequestReasons. Lets staff tell "nothing
    /// matched confidently" apart from "an AI/network call actually failed" at a glance.</summary>
    public string RequestReason { get; set; } = CustomReportRequestReasons.NoConfidentMatch;

    /// <summary>The client's ExtractedSchemaDto, inlined as JSON — mirrors
    /// BlueprintVersion.GeneratedJsonContent's "small JSON inlined in SQL" convention.</summary>
    public string SchemaSnapshotJson { get; set; } = string.Empty;

    public string? SourceFileName { get; set; }

    /// <summary>Set once an analyst fulfills the request and attaches a PowerBiAsset — links to
    /// the SavedReport(PowerBiRequestFulfilled) row created at the same time.</summary>
    public Guid? FulfilledSavedReportId { get; set; }
    public DateTime? FulfilledAtUtc { get; set; }
    public string? FulfilledByEmail { get; set; }

    /// <summary>Set once an admin exports SchemaSnapshotJson to blob storage (the hand-off point
    /// for feeding it into the template-generating agent) — see
    /// AdminReportRequestsController.ExportToBlobAsync.</summary>
    public string? BlobPath { get; set; }
    public DateTime? ExportedToBlobAtUtc { get; set; }
}
