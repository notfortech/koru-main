using System.Text.Json;

namespace StudioTechBI.Application.DTOs.ReportDesigner;

public record GenerateReportModelResponse(
    string CorrelationId,
    long DurationMs,
    JsonElement Blueprint,                              // raw blueprint JSON from stbi_transformers
    string SessionId,
    StarSchemaDto? StarSchema = null,                   // populated when transformers returns star-schema summary
    List<ReportTemplateRecommendation>? Templates = null);

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

