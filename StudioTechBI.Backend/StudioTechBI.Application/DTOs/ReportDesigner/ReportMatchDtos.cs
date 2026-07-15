namespace StudioTechBI.Application.DTOs.ReportDesigner;

public record ReportMatchRequest(string ClientId, ExtractedSchemaDto Schema);

public record DataUsageConsentRequest(string ClientId);

public record DataUsageConsentResponse(Guid DraftId, DateTimeOffset ApprovedAt);

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
/// Result of matching a client's schema against the Approved SchemaModel directory.
/// <see cref="MatchSource"/> reports which path produced this result:
/// "Deterministic" (name-overlap scoring found a confident match on its own),
/// "AiMatched" (deterministic score was below the confidence gate, and the AI
/// picked an existing Approved model instead), or "AiProposedNew" (the AI found
/// no good fit and proposed a brand-new SchemaModel — <see cref="SchemaModelId"/>
/// points at that new row, created with ReviewStatus = PendingReview and excluded
/// from other clients' matching until support approves it).
/// </summary>
public record ReportMatchResultDto(
    Guid DraftId,
    Guid? SchemaModelId,
    string? SchemaModelName,
    string? Industry,
    double Confidence,
    string MatchSource,
    bool PendingSupportReview,
    List<ReportMatchCandidateTemplateDto> CandidateTemplates,
    List<ReportMatchColumnMappingDto> ColumnMappings);
