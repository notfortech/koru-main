namespace StudioTechBI.Application.DTOs.ReportDesigner;

/// <summary>
/// AI-assisted schema/model matching, called by ReportMatchService when the
/// deterministic name-overlap score against the SchemaModel directory falls below
/// its confidence gate. Headers/types only — mirrors stbi_transformers's
/// SchemaModelMatchRequest shape.
/// </summary>
public record SchemaModelAiMatchRequest(
    List<SchemaModelAiMatchColumn> Columns,
    List<SchemaModelAiMatchCandidate> CandidateModels);

public record SchemaModelAiMatchColumn(string ColumnName, string? DataType);

public record SchemaModelAiMatchCandidate(
    string Id,
    string Name,
    string Industry,
    List<SchemaModelAiMatchField> Fields);

public record SchemaModelAiMatchField(string FieldName, string DataType, bool IsRequired);

/// <summary>
/// Exactly one of MatchedModelId or ProposedModel is set — see
/// stbi_transformers's SchemaModelMatchResult for the authoritative contract.
/// </summary>
public record SchemaModelAiMatchResponse(
    Guid? MatchedModelId,
    double Confidence,
    ProposedSchemaModelDto? ProposedModel,
    string? Reasoning);

public record ProposedSchemaModelDto(
    string Name,
    string Industry,
    List<SchemaModelAiMatchField> Fields);
