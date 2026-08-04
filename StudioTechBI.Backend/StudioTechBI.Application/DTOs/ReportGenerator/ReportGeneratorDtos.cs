namespace StudioTechBI.Application.DTOs.ReportGenerator;

public record ReportTemplateDto(
    string Id,
    string Name,
    string? Industry,
    string? Description,
    Dictionary<string, int>? Requires);

public record GeneratedReportDto(
    string? TemplateId,
    string? TemplateName,
    string? PrimaryTable,
    List<ReportKpiDto> Kpis,
    List<ReportChartDto> Charts,
    List<ReportSlicerDto> Slicers,
    Dictionary<string, string> AppliedFilters,
    List<string> Warnings,
    List<Dictionary<string, object?>>? RowData = null,
    string? HtmlTemplateId = null,
    string? HtmlTemplateName = null,
    double? HtmlMatchConfidence = null,
    string? HtmlReport = null);

/// <summary>One HTML report template candidate returned by ReportAgent.Api's
/// /api/reports/match-html-template — the AI-assisted "Verify Template Match" flow's ranked
/// list, before koru-main filters it down to >=0.85 confidence.</summary>
public record HtmlTemplateCandidateDto(
    string TemplateId,
    string TemplateName,
    double Confidence,
    string? Industry);

/// <summary>One manifest pushed to ReportAgent.Api's registry cache. ManifestJson is forwarded
/// as-is — koru-main doesn't need to parse the manifest's own schema, only the raw blob text it
/// already downloaded while syncing from the report-templates catalog.</summary>
public record HtmlTemplateManifestPushDto(string Id, string ManifestJson);

/// <summary>Response for POST /api/report-generator/verify-html-match — Candidates is already
/// filtered to >=0.85 confidence server-side (see ReportGeneratorController.HtmlMatchConfidenceThreshold),
/// so the frontend never has to duplicate that threshold logic.</summary>
public record VerifyHtmlTemplateMatchResponse(
    string CorrelationId,
    List<HtmlTemplateCandidateDto> Candidates);

public record ReportSlicerDto(
    string Column,
    List<string> Values);

public record ReportKpiDto(
    string Label,
    double Value,
    string Column,
    string Aggregation,
    double? Change = null,
    string? Unit = null);

public record ReportChartDto(
    string Type,
    string Title,
    List<string>? X,
    List<string>? Categories,
    List<ReportChartSeriesDto> Series);

public record ReportChartSeriesDto(
    string Name,
    List<double> Values,
    string? Unit = null,
    List<double>? PercentOfTotal = null);
