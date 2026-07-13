namespace StudioTechBI.Application.DTOs.ReportDesigner;

public record ReportMatchRequest(string ClientId, ExtractedSchemaDto Schema);

public record ReportMatchColumnMappingDto(
    string FieldName,
    string DataType,
    bool IsRequired,
    string? ClientColumnName,
    bool Included);

public record ReportMatchCandidateTemplateDto(
    Guid TemplateId,
    string TemplateName,
    bool IsPublishReady);

/// <summary>
/// Result of matching a client's schema against the Approved SchemaModel directory. Deterministic
/// (.NET) matching only in this version — no AI call is made here yet. When AI-proposed-new-model
/// support is added, SchemaModelId will be null and IsAiSuggested/PendingReview will surface a
/// newly-created SchemaModel instead of falling back to "no match" as it does today.
/// </summary>
public record ReportMatchResultDto(
    Guid DraftId,
    Guid? SchemaModelId,
    string? SchemaModelName,
    string? Industry,
    double Confidence,
    List<ReportMatchCandidateTemplateDto> CandidateTemplates,
    List<ReportMatchColumnMappingDto> ColumnMappings);
