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
    int Failed,
    bool DryRun = false);

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
    string? ErrorMessage,
    bool HasReferenceDataset = false);

public record HtmlTemplateManifestResponseDto(string TemplateId, string ManifestJson);

public record HtmlTemplateManifestUpdateRequestDto(string ManifestJson);

/// <summary>Result of deriving proposed manifest.json fields (requiredColumns, requires.min*,
/// dataContract.rowFields) from an admin-supplied reference dataset -- ManifestJson is a proposed,
/// UNSAVED merge the frontend pre-fills the existing manifest editor with; saving still goes
/// through UpdateManifestAsync unchanged. Warnings flag anything already in the template's manifest
/// that the reference dataset didn't account for (e.g. a requiredColumns entry with no matching
/// column in the file), surfaced for the admin to review rather than silently dropped.</summary>
public record HtmlTemplateReferenceDatasetDeriveResponseDto(
    string TemplateId,
    bool Success,
    string? ManifestJson,
    List<string> Warnings,
    List<string> Errors);

/// <summary>Result of the admin "Preview" action -- runs the real deterministic match+render
/// pipeline against the template's stored reference dataset and proves it resolves back to this
/// same template id.</summary>
public record HtmlTemplatePreviewResponseDto(
    string TemplateId,
    bool Matched,
    string? RenderedHtml,
    List<string> Warnings,
    string? ErrorMessage);
