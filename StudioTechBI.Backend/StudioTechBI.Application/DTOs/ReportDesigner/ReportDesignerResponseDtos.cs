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

public record ReportDesignerConsentResponse(
    bool Granted,
    string SchemaHash,
    DateTimeOffset DecidedAt);

// ── S7/S9: TMDL authoring (stbi_transformers) ────────────────────────────────────────────────

public record TmdlFileDto(string Path, string Content);

public record TmdlValidationDto(bool IsValid, List<string> Violations);

public record AuthorTmdlResponse(
    List<TmdlFileDto> Files,
    string Reasoning,
    TmdlValidationDto? Validation);

// ── S9: publish (author-tmdl -> validate -> deploy, chained; or rebind an existing template) ──

/// <summary>
/// "MatchedTemplate": rebound an existing, already-published template's dataset to this client
/// (no new TMDL authored — TmdlFileCount is null). "GeneratedFromBlueprint": the original path,
/// a brand-new semantic model was authored and deployed.
/// </summary>
public static class PublishSource
{
    public const string MatchedTemplate = "MatchedTemplate";
    public const string GeneratedFromBlueprint = "GeneratedFromBlueprint";
}

public record PublishReportResponse(
    string CorrelationId,
    string WorkspaceId,
    string WorkspaceName,
    string DatasetId,
    string DatasetName,
    bool DatasetCreated,
    int? TmdlFileCount,
    List<string> DeploySteps,
    string Source);

