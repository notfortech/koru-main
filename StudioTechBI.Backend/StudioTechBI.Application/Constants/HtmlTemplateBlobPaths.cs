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

    public static string ManifestPath(string templateId) => $"templates/html/{templateId}/manifest.json";
    public static string ChromeHtmlPath(string templateId) => $"templates/html/{templateId}/chrome.html";
    public static string PreviewPath(string templateId) => $"templates/html/{templateId}/preview.jpg";
}
