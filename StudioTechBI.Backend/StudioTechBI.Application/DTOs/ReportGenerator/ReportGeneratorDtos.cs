using System.Text.Json;
using StudioTechBI.Application.DTOs.DashboardTemplate;
using StudioTechBI.Application.DTOs.VisualPlan;

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
    // Raw passthrough of the Python engine's own "rowData" JSON — either a flat array (single-
    // table templates, the original shape) or an object keyed by table alias (multi-table
    // templates, one array per declared sheet; see row_export.py's module docstring). A JsonElement
    // round-trips whichever shape arrived without koru-main needing to know or care which one it
    // is -- HtmlReportAssemblyService injects it into the chrome verbatim via GetRawText().
    JsonElement? RowData = null,
    string? HtmlTemplateId = null,
    string? HtmlTemplateName = null,
    double? HtmlMatchConfidence = null,
    string? HtmlReport = null,
    // AI-assisted "closest template" blend fallback (ReportGeneratorController.GenerateAsync):
    // populated only when no confident HTML match existed but a closest role-gate-passing
    // candidate was found and blended with mock data for the gaps. HtmlTemplateId/HtmlReport stay
    // null in this case by design -- this fallback always renders as the classic KPI/chart view,
    // never the branded HTML chrome, and never enables Save Report.
    string? ClosestTemplateId = null,
    string? ClosestTemplateName = null,
    List<ProvenanceEntryDto>? BlendProvenance = null,
    // Base64-encoded .xlsx bytes -- returned inline rather than a blob/SAS URL, since the blended
    // file is never persisted server-side (only its schema is, via LogClosestTemplateBlendAsync).
    // The frontend decodes this directly into a Blob and downloads it with no extra network hop.
    string? BlendedDatasetFileBase64 = null,
    string? BlendNote = null,
    // POST /api/report-generator/generate-preview only (see ReportGeneratorController): the
    // AgentHost-proposed chart spec per chart in Charts above (chart type, measure/dimension,
    // drill path, filter field, value kind, pairing) -- everything the frontend needs to build
    // cross-filter/drill-down UI. Stays null on every other path (unchanged behavior).
    List<VisualPlanChartSpecDto>? ChartPlan = null);

/// <summary>Optional brand-color override for an HTML report's chrome.html, resolved from the
/// frontend's existing theme picker (reportThemes.tsx). Hex strings or null per slot; null means
/// "leave that slot at the template's own default". Passed alongside GeneratedReportDto to
/// IHtmlReportAssemblyService.AssembleAsync -- deliberately not a GeneratedReportDto field, since
/// the frontend already knows these values and round-tripping them back would be pointless.
/// </summary>
public record ReportThemeOverride(string? Primary, string? Dark, string? Light, string? Bg);

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

/// <summary>One column profiled by /api/reports/profile-columns, via the exact same profiler
/// (lib/generic_report_engine.profile_table) the deterministic matcher's role gate runs at real
/// match time -- keeps a template manifest's derived requires.min* counts consistent with what
/// will actually be evaluated later.</summary>
public record ProfiledColumnDto(string Name, string Role);

public record ColumnProfileCountsDto(int MinNumeric, int MinDate, int MinCategorical);

/// <summary>Columns is the flat union across every table/sheet in the reference file; Counts is
/// summed the same way (both mirror profile_tables()'s real, unfiltered output). Tables is the
/// per-sheet breakdown, keyed by sheet name, used to reconcile a multi-table manifest's
/// dataContract.tables[] against the correctly-named sheet instead of one blanket list.
/// PrimaryTable is the one table pick_primary_table()/export_row_data() actually use for a
/// SINGLE-table manifest's rowFields export -- that merge must scope to just this table's columns,
/// never the flat cross-sheet union.</summary>
public record ColumnProfileResultDto(
    List<ProfiledColumnDto> Columns,
    ColumnProfileCountsDto Counts,
    Dictionary<string, List<ProfiledColumnDto>> Tables,
    string PrimaryTable);

// ── POST /api/reports/chart-from-spec (ReportAgent.Api) ─────────────────────────────────────────
// Computes real chart values from an AgentHost-proposed chart spec against the uploaded file.
// Backs ReportGeneratorController's internal/QA-only "generate-preview" endpoint.

/// <summary>Multipart "chartSpecs" field shape ReportAgent.Api's /api/reports/chart-from-spec
/// expects -- camelCase, and (unlike VisualPlanChartSpecDto) no Title, since the compute engine
/// doesn't need a display label. ReportGeneratorClient.GenerateChartsFromSpecAsync maps each
/// AgentHost-returned VisualPlanChartSpecDto into one of these before sending.</summary>
public record ChartFromSpecRequestItemDto(
    string Id,
    string Measure,
    string Dimension,
    string ChartType,
    string ValueKind,
    List<string>? DrillPath,
    string? FilterField,
    string? PairId,
    List<string>? DrillFilters = null);

/// <summary>One computed chart from /api/reports/chart-from-spec -- the same shape as
/// ReportChartDto (Type/Title/X/Categories/Series) plus pass-through spec metadata so the caller
/// can line each computed chart back up with its originating chart spec.</summary>
public record ReportChartFromSpecDto(
    string Type,
    string Title,
    List<string>? X,
    List<string>? Categories,
    List<ReportChartSeriesDto> Series,
    string? Id = null,
    string? FilterField = null,
    List<string>? DrillPath = null,
    string? ValueKind = null,
    string? PairId = null);

/// <summary>Response body of /api/reports/chart-from-spec. RowData mirrors GeneratedReportDto's
/// own RowData field (raw passthrough JsonElement -- flat array or per-table object, see that
/// field's remarks) and is always populated here (capped/nullable exactly as this response already
/// provides it), unlike the HTML-template-matched /generate path where it's only sometimes set.</summary>
public record ChartFromSpecResultDto(
    List<ReportChartFromSpecDto> Charts,
    JsonElement? RowData,
    List<string> Warnings);

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

// ── Large-file upload (direct-to-blob + durable async processing, Phase 1) ─────────────────────

public record ReportUploadInitRequest(string FileName);

public record ReportUploadInitResponse(
    Guid JobId,
    string WriteUrl,
    string BlobPath,
    DateTimeOffset ExpiresAtUtc);

/// <summary>Same optional fields the synchronous /generate endpoint already accepts as form
/// fields, carried here as a JSON body since no file rides along on this call — the file already
/// landed in blob during the direct upload step.</summary>
public record ReportUploadCompleteRequest(
    string? TemplateId,
    string? Filters,
    string? HtmlTemplateId,
    string? Mode,
    bool IsRefinement,
    string? ThemePrimary,
    string? ThemeDark,
    string? ThemeLight,
    string? ThemeBg);

public record ReportUploadCompleteResponse(Guid JobId, string Status);

/// <summary>Poll response for GET /api/report-generator/jobs/{jobId}. Result is populated only
/// once Status is Completed; ErrorMessage only once Failed.</summary>
public record ReportGenerationJobStatusResponse(
    Guid JobId,
    string Status,
    GeneratedReportDto? Result,
    string? ErrorMessage);
