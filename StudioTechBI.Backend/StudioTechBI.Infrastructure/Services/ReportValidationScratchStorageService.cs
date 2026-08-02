using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.Infrastructure.Services;

/// <summary>
/// Stores the one source file a Report Validation run needs, for the duration of that run only.
/// Uses Azure Blob Storage when AzureBlob:ConnectionString is configured; falls back to the local
/// filesystem for development — same convention as BlueprintStorageService, container/prefix kept
/// separate so it's obvious at a glance which blobs are durable artefacts vs. run-scoped scratch.
/// </summary>
public class ReportValidationScratchStorageService : IReportValidationScratchStorageService
{
    private const string ContainerName = "report-validation-scratch";

    private readonly BlobContainerClient? _containerClient;
    private readonly string _localBasePath;
    private readonly ILogger<ReportValidationScratchStorageService> _logger;

    public ReportValidationScratchStorageService(IConfiguration configuration, ILogger<ReportValidationScratchStorageService> logger)
    {
        _logger = logger;

        _localBasePath = configuration["LocalStorage:ReportValidationScratchPath"]
            ?? Path.Combine(Path.GetTempPath(), "koru-report-validation-scratch");

        var connectionString = configuration["AzureBlob:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            var serviceClient = new BlobServiceClient(connectionString);
            _containerClient = serviceClient.GetBlobContainerClient(ContainerName);
        }
        else
        {
            _logger.LogWarning(
                "AzureBlob:ConnectionString not configured. Report Validation scratch files will be stored locally at {Path}.",
                _localBasePath);
        }
    }

    public async Task<string> StoreAsync(Guid runId, string fileName, Stream fileStream, CancellationToken cancellationToken = default)
    {
        var safeName = Path.GetFileName(fileName);
        var blobPath = $"{runId}/{safeName}";

        if (_containerClient != null)
        {
            await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            var client = _containerClient.GetBlobClient(blobPath);
            await client.UploadAsync(fileStream, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/octet-stream" }
            }, cancellationToken);

            _logger.LogInformation("Stored Report Validation scratch file: {BlobPath}", blobPath);
            return blobPath;
        }

        var localPath = LocalPath(blobPath);
        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
        await using (var fs = File.Create(localPath))
        {
            await fileStream.CopyToAsync(fs, cancellationToken);
        }
        _logger.LogInformation("Stored Report Validation scratch file locally: {LocalPath}", localPath);
        return blobPath;
    }

    public async Task<Stream?> GetAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        if (_containerClient != null)
        {
            var client = _containerClient.GetBlobClient(blobPath);
            if (!await client.ExistsAsync(cancellationToken)) return null;
            var response = await client.DownloadStreamingAsync(cancellationToken: cancellationToken);
            return response.Value.Content;
        }

        var localPath = LocalPath(blobPath);
        return File.Exists(localPath) ? File.OpenRead(localPath) : null;
    }

    public async Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        if (_containerClient != null)
        {
            await _containerClient.GetBlobClient(blobPath).DeleteIfExistsAsync(cancellationToken: cancellationToken);
            return;
        }

        var localPath = LocalPath(blobPath);
        if (File.Exists(localPath)) File.Delete(localPath);
    }

    private string LocalPath(string blobPath) =>
        Path.Combine(_localBasePath, blobPath.Replace('/', Path.DirectorySeparatorChar));
}
