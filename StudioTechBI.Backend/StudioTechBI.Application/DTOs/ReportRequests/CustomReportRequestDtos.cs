using StudioTechBI.Application.DTOs.ReportDesigner;

namespace StudioTechBI.Application.DTOs.ReportRequests;

/// <summary>Body for POST /api/report-requests. Schema is the client's already-extracted
/// ExtractedSchemaDto (same shape ReportDesignerController's extract-schema/* endpoints return) —
/// sent as-is rather than re-uploading the source file, since the schema snapshot is all a
/// bespoke Power BI build needs to get started. Reason is one of CustomReportRequestReasons
/// (NoConfidentMatch | GenerationError); unrecognized/omitted values default to NoConfidentMatch
/// server-side.</summary>
public record CreateCustomReportRequestDto(ExtractedSchemaDto Schema, string? Notes, string? Reason = null);

public record CreateCustomReportRequestResponse(Guid RequestId);

public record CustomReportRequestSummaryDto(
    Guid RequestId,
    string Status,
    string RequestReason,
    string? RequestedByEmail,
    string? SourceFileName,
    DateTime CreatedAt,
    DateTime? FulfilledAtUtc,
    DateTime? ExportedToBlobAtUtc);

public record CustomReportRequestDetailDto(
    Guid RequestId,
    Guid ClientId,
    string Status,
    string RequestReason,
    string? RequestedByEmail,
    string? Notes,
    ExtractedSchemaDto? Schema,
    string? SourceFileName,
    DateTime CreatedAt,
    Guid? FulfilledSavedReportId,
    DateTime? FulfilledAtUtc,
    string? FulfilledByEmail,
    string? BlobPath,
    DateTime? ExportedToBlobAtUtc);

public record FulfillCustomReportRequestDto(Guid PowerBiAssetId);

public record ExportCustomReportRequestToBlobResponse(string BlobPath, DateTime ExportedToBlobAtUtc);
