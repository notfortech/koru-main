namespace StudioTechBI.Application.DTOs.ReportDesigner;

public record GenerateReportModelResponse(
    StarSchemaDto StarSchema,
    List<ReportTemplateRecommendation> Templates,
    string CorrelationId,
    long DurationMs);

public record StarSchemaDto(
    string FactTable,
    List<string> DimensionTables,
    List<RelationshipDto> Relationships);

public record RelationshipDto(
    string FromTable,
    string FromColumn,
    string ToTable,
    string ToColumn);

public record ReportTemplateRecommendation(
    string ThemeId,
    string ThemeName,
    double Score);
