using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.Infrastructure.Services;

public sealed class ReportTemplateAssetService : IReportTemplateAssetService
{
    private const string ContainerName = "report-templates";
    private readonly BlobContainerClient? _containerClient;
    private readonly ILogger<ReportTemplateAssetService> _logger;

    public ReportTemplateAssetService(IConfiguration configuration, ILogger<ReportTemplateAssetService> logger)
    {
        _logger = logger;
        var connectionString = configuration["AzureBlob:ConnectionString"];
        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogWarning("Azure Blob connection string not configured. Report template assets will be unavailable.");
            _containerClient = null;
            return;
        }

        var blobServiceClient = new BlobServiceClient(connectionString);
        _containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
    }

    public async Task<(Stream stream, string contentType)?> DownloadTemplateScreenshotAsync(
        string blobName,
        CancellationToken cancellationToken = default)
    {
        if (_containerClient == null) return null;

        var name = (blobName ?? "").Trim().Replace("\\", "/");
        if (name.Length == 0) return null;

        var client = _containerClient.GetBlobClient(name);
        if (!await client.ExistsAsync(cancellationToken))
            return null;

        var resp = await client.DownloadStreamingAsync(cancellationToken: cancellationToken);
        var ct = resp.Value.Details.ContentType;
        if (string.IsNullOrWhiteSpace(ct))
        {
            // default for our convention
            ct = "image/jpeg";
        }

        return (resp.Value.Content, ct);
    }
}

