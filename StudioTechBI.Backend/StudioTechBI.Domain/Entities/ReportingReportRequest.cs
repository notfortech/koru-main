namespace StudioTechBI.Domain.Entities;

/// <summary>Pending request to generate/provision a report for a client/template (reporting.ReportRequests).</summary>
public sealed class ReportingReportRequest
{
    public Guid RequestId { get; set; }
    public Guid ClientId { get; set; }
    public Guid TemplateId { get; set; }

    /// <summary>Blob path under clients container pointing at the user's uploaded file in accounting/created/.</summary>
    public string? SourceBlobPath { get; set; }

    /// <summary>Workflow status (e.g. Pending, Processing, Completed, Failed).</summary>
    public string Status { get; set; } = "Pending";

    public string? RequestedByEmail { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

