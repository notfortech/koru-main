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
