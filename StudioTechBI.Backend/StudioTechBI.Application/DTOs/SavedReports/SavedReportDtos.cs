namespace StudioTechBI.Application.DTOs.SavedReports;

/// <summary>Body for POST /api/saved-reports. HtmlReport is the exact string already on screen
/// (per HtmlReportAssemblyService) — this endpoint never re-assembles, only persists.</summary>
public record SaveReportRequest(
    string HtmlReport,
    string? TemplateId,
    string? TemplateName,
    string? HtmlTemplateId,
    string? HtmlTemplateName,
    string? SourceFileName,
    Dictionary<string, string>? AppliedFilters);

public record SaveReportResponse(Guid SavedReportId, int VersionNumber);

public record SavedReportSummaryDto(
    Guid SavedReportId,
    string Title,
    string SourceType,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    /// <summary>Set only for SourceType == PowerBiRequestFulfilled.</summary>
    Guid? PowerBiAssetId);

public record SavedReportDetailDto(
    Guid SavedReportId,
    string Title,
    string SourceType,
    string Status,
    /// <summary>The active version's rendered HTML — populated only for SourceType ==
    /// GeneratedHtml. Null for PowerBiRequestFulfilled, which renders through the existing
    /// Power BI embed-token flow instead.</summary>
    string? HtmlReport,
    Guid? PowerBiAssetId,
    DateTime CreatedAt);
