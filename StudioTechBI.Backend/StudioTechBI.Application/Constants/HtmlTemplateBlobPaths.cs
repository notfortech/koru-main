namespace StudioTechBI.Application.Constants;

/// <summary>
/// Single source of truth for the blob path convention interactive HTML report templates live
/// under (container "clients", see BlobStorageService) -- shared by the sync runner, the admin
/// upload/edit service, and anything else that needs to read or write this content, so the
/// convention can never drift between call sites.
/// </summary>
public static class HtmlTemplateBlobPaths
{
    public const string IndexPath = "templates/html/index.json";

    /// <summary>
    /// Ids an admin has explicitly deleted that also happen to be built-in seed templates
    /// (HtmlTemplateSeedCatalog) -- those are compiled into the assembly, not blob-backed, so
    /// un-listing them from IndexPath alone doesn't stop HtmlTemplateSyncRunner from re-adding
    /// them every cycle. This is the only durable record of "this seed id was explicitly retired."
    /// </summary>
    public const string ExcludedSeedIdsPath = "templates/html/excluded-seed-ids.json";

    public static string ManifestPath(string templateId) => $"templates/html/{templateId}/manifest.json";
    public static string ChromeHtmlPath(string templateId) => $"templates/html/{templateId}/chrome.html";
    public static string PreviewPath(string templateId) => $"templates/html/{templateId}/preview.jpg";

    /// <summary>
    /// The literal HTML comment HtmlReportAssemblyService substitutes with the injected
    /// &lt;script type="application/json" id="stbi-report-data"&gt; data block. Shared with
    /// HtmlTemplateAdminService's upload/edit validation so "this template passed validation"
    /// actually guarantees "this template will receive real data" -- a chrome.html that merely
    /// references the id (e.g. its own pre-authored, empty `id="stbi-report-data"` script tag,
    /// without this literal marker) used to pass a looser substring check at upload time, then
    /// silently fail at assembly time: the marker-missing fallback appends a second element with
    /// the same id right before &lt;/body&gt;, but getElementById returns the FIRST match in
    /// document order -- the template author's own empty one -- so the report always renders with
    /// zero rows even though the match/upload both looked successful.
    /// </summary>
    public const string DataMarker = "<!--STBI_REPORT_DATA-->";
}
