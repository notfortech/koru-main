namespace StudioTechBI.Application.Interfaces;

public interface IReportTemplateAssetService
{
    /// <summary>Downloads a template screenshot from the report-templates container.</summary>
    Task<(Stream stream, string contentType)?> DownloadTemplateScreenshotAsync(string blobName, CancellationToken cancellationToken = default);
}

