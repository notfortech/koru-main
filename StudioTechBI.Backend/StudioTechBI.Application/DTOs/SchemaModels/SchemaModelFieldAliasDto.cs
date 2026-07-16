namespace StudioTechBI.Application.DTOs.SchemaModels;

/// <summary>Admin-facing view of a learned column alias, joined with its field/model for display.</summary>
public record SchemaModelFieldAliasDto(
    Guid Id,
    Guid SchemaModelFieldId,
    string FieldName,
    Guid SchemaModelId,
    string SchemaModelName,
    string Industry,
    string AliasName,
    string? ObservedDataType,
    double Confidence,
    string Source,
    string ApprovalStatus,
    int ObservedCount,
    DateTime FirstSeenAt,
    DateTime LastSeenAt,
    string? DecidedBy,
    DateTime? DecidedAt);
