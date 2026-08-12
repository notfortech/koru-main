namespace StudioTechBI.Application.Constants;

/// <summary>
/// Single source of truth for the compile-time upload-size ceiling used by every
/// <c>[RequestSizeLimit]</c> attribute across the API's file-accepting controllers.
/// ASP.NET Core requires attribute arguments to be compile-time constants, so this value can't
/// be config-driven directly -- <see cref="Options.UploadLimitsOptions"/> (bound from
/// configuration) is the actually-configurable limit each controller checks against at runtime;
/// it can tighten this ceiling via config but can never exceed it without a recompile.
/// </summary>
public static class UploadLimits
{
    public const long MaxUploadBytes = 50L * 1024L * 1024L; // 50 MB

    /// <summary>
    /// Bulk HTML report template (.zip) uploads -- admin-only, trusted content, infrequent, and
    /// naturally bulkier than a single client data file (many manifest.json + chrome.html pairs
    /// per zip), so this stays a separate, larger ceiling rather than sharing MaxUploadBytes.
    /// </summary>
    public const long MaxHtmlTemplateBatchBytes = 200L * 1024L * 1024L; // 200 MB

    /// <summary>
    /// Single-file replacement of one existing HTML template's chrome.html -- always a single
    /// self-contained HTML document, never bundled with a manifest/preview, so a much smaller
    /// ceiling than the batch zip is appropriate.
    /// </summary>
    public const long MaxHtmlTemplateSingleFileBytes = 20L * 1024L * 1024L; // 20 MB
}
