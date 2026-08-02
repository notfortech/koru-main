namespace StudioTechBI.Application.DTOs.ReportValidation;

public record ReportValidationCheckDto(
    string CheckFamily,
    string CheckName,
    string Status,
    string? Detail,
    object? Evidence);

public record ReportValidationRunDto(
    Guid RunId,
    string Status,
    string? OverallResult,
    string? TemplateId,
    string? TemplateName,
    List<ReportValidationCheckDto> Checks,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string? ErrorMessage);

/// <summary>Summary row for the history list — full check detail is fetched per-run via
/// GET report-validation/runs/{id} on drill-down.</summary>
public record ReportValidationRunSummaryDto(
    Guid RunId,
    string Status,
    string? OverallResult,
    string? TemplateName,
    DateTime CreatedAt,
    DateTime? CompletedAt);

public record StartReportValidationResponse(Guid RunId, string Status);
