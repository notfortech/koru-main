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
    List<string> Warnings);

public record ReportKpiDto(
    string Label,
    double Value,
    string Column,
    string Aggregation);

public record ReportChartDto(
    string Type,
    string Title,
    List<string>? X,
    List<string>? Categories,
    List<ReportChartSeriesDto> Series);

public record ReportChartSeriesDto(
    string Name,
    List<double> Values);
