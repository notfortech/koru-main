using StudioTechBI.Application.DTOs.Admin;

namespace StudioTechBI.Application.Interfaces;

/// <summary>
/// Admin-facing content management for interactive HTML report templates -- the "zero code
/// changes, ever, to add/edit/retire a template" surface. Every write path validates before it
/// ever touches the live blob path (see HtmlTemplateAdminService), and every successful write
/// re-triggers IHtmlTemplateSyncRunner so the change is live immediately rather than waiting on
/// the background sync's own interval.
/// </summary>
public interface IHtmlTemplateAdminService
{
    /// <summary>
    /// Accepts a .zip in the shape already produced by template-authoring tooling today: one
    /// manifest.json + chrome.html (+ optional preview.jpg) per template, at any nesting depth or
    /// wrapper folder name, plus optionally one template loose at the zip root. The zip's own
    /// index.json (if present) is ignored -- templates/html/index.json is always merged, never
    /// replaced.
    /// </summary>
    Task<HtmlTemplateBulkUploadResponseDto> UploadBatchAsync(Stream zipStream, CancellationToken cancellationToken = default);

    Task<List<HtmlTemplateSummaryDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Un-lists a template from index.json. Never deletes the underlying blob files.</summary>
    Task DeleteAsync(string templateId, CancellationToken cancellationToken = default);

    /// <summary>Runs the sync cycle immediately; returns how many templates were pushed.</summary>
    Task<int> ForceSyncNowAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the current manifest.json, pretty-printed, or null if the template doesn't exist.</summary>
    Task<string?> GetManifestRawAsync(string templateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and, only on success, overwrites one template's manifest.json in place (chrome.html
    /// is untouched). Rejects a manifest whose own "id" doesn't match <paramref name="templateId"/> --
    /// renaming is a delete-and-recreate, not an edit, to avoid orphaning index.json.
    /// </summary>
    Task<HtmlTemplateUploadResultDto> UpdateManifestAsync(string templateId, string manifestJson, CancellationToken cancellationToken = default);
}
