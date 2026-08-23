using StudioTechBI.Application.DTOs.ReportDesigner;

namespace StudioTechBI.Application.DTOs.VisualPlan;

/// <summary>
/// Request posted to STBI-AgentHost POST /api/visual-plan/generate (aliased /api/visual-plans).
/// Carries column structure and a handful of sample rows per table -- never full data -- plus the
/// already-generated star schema (see ReportDesignerController.GenerateReportModelAsync), so
/// AgentHost can propose a chart plan without koru-main deriving any of that logic itself. Backs
/// ReportGeneratorController's internal/QA-only "generate-preview" endpoint.
/// </summary>
public record VisualPlanGenerationRequest(
    List<VisualPlanTableDto> Tables,
    StarSchemaDto? StarSchema);

/// <summary>One profiled table's column structure plus a small sample of its actual rows (capped
/// at a handful per table -- see ReportGeneratorController.SampleRowsPerTable). Columns come from
/// IReportGeneratorClient.ProfileColumnsAsync's existing per-table breakdown; this DTO only adds
/// the sample rows AgentHost needs alongside that structure.</summary>
public record VisualPlanTableDto(
    string TableName,
    List<VisualPlanColumnDto> Columns,
    List<Dictionary<string, string>> SampleRows);

public record VisualPlanColumnDto(string Name, string Role);

/// <summary>One chart spec as returned by AgentHost's visual-plan generator (PascalCase wire
/// shape). Reused verbatim as GeneratedReportDto.ChartPlan -- it already carries everything the
/// frontend needs to build cross-filter/drill-down UI (chart type, measure/dimension, drill path,
/// filter field, value kind, pairing) without koru-main needing a second, duplicate DTO.</summary>
public record VisualPlanChartSpecDto(
    string Id,
    string Title,
    string ChartType,
    string Measure,
    string Dimension,
    List<string>? DrillPath,
    string? FilterField,
    string ValueKind,
    string? PairId);
