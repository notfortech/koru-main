namespace StudioTechBI.Application.DTOs.Admin;

/// <summary>Outcome of attempting to create/update one template within a batch or single-manifest edit.</summary>
public record HtmlTemplateUploadResultDto(
    string TemplateId,
    string Status, // "Created" | "Updated" | "Failed"
    List<string> Errors,
    List<string> Warnings);

public record HtmlTemplateBulkUploadResponseDto(
    List<HtmlTemplateUploadResultDto> Results,
    int Created,
    int Updated,
    int Failed);

/// <summary>Row shown on the admin HTML Templates list -- projected from index.json + each manifest.json.</summary>
public record HtmlTemplateSummaryDto(
    string Id,
    string Name,
    string Industry,
    List<string> RequiredColumns,
    List<string> OptionalColumns,
    bool HasPreview,
    bool HasThemeSlots,
    bool HasError,
    string? ErrorMessage);

public record HtmlTemplateManifestResponseDto(string TemplateId, string ManifestJson);

public record HtmlTemplateManifestUpdateRequestDto(string ManifestJson);
