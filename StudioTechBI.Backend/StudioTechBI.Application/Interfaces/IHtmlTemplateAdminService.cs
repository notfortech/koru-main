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
    /// <param name="dryRun">
    /// When true, runs every validation check and reports what would happen (including which
    /// template ids already exist and would be overwritten) without writing anything to blob
    /// storage or index.json -- lets a caller confirm an overwrite before it actually happens.
    /// </param>
    Task<HtmlTemplateBulkUploadResponseDto> UploadBatchAsync(Stream zipStream, bool dryRun = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces just the chrome.html of an already-existing template (its manifest.json is
    /// untouched) -- the single-file complement to UpdateManifestAsync, for when only the markup
    /// needs a fix and building a full zip is overkill. Fails if the template id isn't already
    /// listed; use UploadBatchAsync to create a brand new template.
    /// </summary>
    Task<HtmlTemplateUploadResultDto> UploadChromeHtmlAsync(string templateId, string chromeHtml, CancellationToken cancellationToken = default);

    Task<List<HtmlTemplateSummaryDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Un-lists a template from index.json. Never deletes the underlying blob files.
    /// Returns false (no-op) when templateId wasn't listed in the first place, so the caller can
    /// tell an already-gone template apart from a fresh removal.</summary>
    Task<bool> DeleteAsync(string templateId, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Stores the admin-supplied reference dataset (the real xlsx/csv the template's dashboard was
    /// built from) for this template, profiles its columns via the same profiler the deterministic
    /// matcher's role gate uses at real match time, and returns a proposed, merged manifest.json --
    /// existing dataContract.rowFields aliases are preserved (chrome.html depends on them staying
    /// stable), only new columns are appended. The caller must still call UpdateManifestAsync to
    /// actually save it; this never writes the manifest itself. The raw file is stored immediately
    /// regardless, since it also backs PreviewWithReferenceDatasetAsync.
    /// </summary>
    Task<HtmlTemplateReferenceDatasetDeriveResponseDto> DeriveManifestFromReferenceDatasetAsync(
        string templateId, Stream fileStream, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-runs the deterministic match+render pipeline against this template's stored reference
    /// dataset and asserts it resolves back to this same template id -- the admin-facing proof that
    /// a client's matching file will actually be picked up by the no-AI path. No SavedReport/job
    /// history/credit-ledger side effects.
    /// </summary>
    Task<HtmlTemplatePreviewResponseDto> PreviewWithReferenceDatasetAsync(
        string templateId, CancellationToken cancellationToken = default);
}
