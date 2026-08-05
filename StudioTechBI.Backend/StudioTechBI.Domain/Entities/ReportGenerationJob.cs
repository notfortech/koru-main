namespace StudioTechBI.Domain.Entities;

/// <summary>
/// Tracks one async, large-file Report Generator job (Pending → Processing → Completed/Failed).
/// Exists only for uploads that went through the direct-to-blob path (see
/// ReportGeneratorController's uploads/init + uploads/{id}/complete endpoints) — small files still
/// go through the original synchronous /generate endpoint unchanged and never create a row here.
/// BlobPath points at the client's already-uploaded source file (verified server-side via
/// BlobClient.GetPropertiesAsync before this row is created); ResultJson holds the computed
/// GeneratedReportDto, verbatim JSON, once Completed, mirroring BlueprintGeneration's own
/// "worker is self-contained, state lives on the row" shape.
/// </summary>
public class ReportGenerationJob : BaseEntity
{
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;

    public string BlobPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;

    /// <summary>Pending | Processing | Completed | Failed</summary>
    public string Status { get; set; } = ReportGenerationJobStatuses.Pending;

    /// <summary>JSON-serialised {templateId, filters, htmlTemplateId, mode, themePrimary,
    /// themeDark, themeLight, themeBg} — same fields /generate already accepts, so the worker can
    /// call IReportGeneratorClient without re-deriving anything from the original HTTP request.</summary>
    public string? RequestPayloadJson { get; set; }

    /// <summary>The computed GeneratedReportDto, verbatim JSON, once Status is Completed.</summary>
    public string? ResultJson { get; set; }

    public string? ErrorMessage { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public string? CorrelationId { get; set; }
}

public static class ReportGenerationJobStatuses
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}
