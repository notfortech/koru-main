namespace StudioTechBI.Application.DTOs.SchemaModels;

public record SchemaModelFieldDto(
    string FieldName,
    string DataType,
    bool IsRequired,
    int SortOrder);

public record SchemaModelDto(
    Guid Id,
    string Name,
    string Industry,
    string? Description,
    List<SchemaModelFieldDto> Fields,
    List<Guid> TemplateIds,
    bool IsAiSuggested = false,
    string ReviewStatus = "Approved",
    Guid? SuggestedByClientId = null);
