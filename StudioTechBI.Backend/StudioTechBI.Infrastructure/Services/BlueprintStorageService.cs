using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.Infrastructure.Services;

/// <summary>
/// Stores Blueprint artefacts (Analytics Deployment Contract JSON, Blueprint PDF).
/// Uses Azure Blob Storage when AzureBlob:ConnectionString is configured;
/// falls back to the local filesystem for development (UseDemoStorage=true).
/// Binary data is never stored in SQL.
/// </summary>
public class BlueprintStorageService : IBlueprintStorageService
{
    private const string ContainerName = "blueprints";

    private readonly BlobContainerClient? _containerClient;
    private readonly string _localBasePath;
    private readonly ILogger<BlueprintStorageService> _logger;

    public BlueprintStorageService(IConfiguration configuration, ILogger<BlueprintStorageService> logger)
    {
        _logger = logger;

        _localBasePath = configuration["LocalStorage:BlueprintsPath"]
            ?? Path.Combine(Path.GetTempPath(), "koru-blueprints");

        var connectionString = configuration["AzureBlob:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            var serviceClient = new BlobServiceClient(connectionString);
            _containerClient = serviceClient.GetBlobContainerClient(ContainerName);
        }
        else
        {
            _logger.LogWarning(
                "AzureBlob:ConnectionString not configured. Blueprint artefacts will be stored locally at {Path}.",
                _localBasePath);
        }
    }

    public async Task<string> StoreJsonAsync(
        Guid blueprintId,
        int versionNumber,
        string json,
        CancellationToken cancellationToken = default)
    {
        var blobPath = $"{blueprintId}/v{versionNumber}/contract.json";

        if (_containerClient != null)
        {
            await EnsureContainerExistsAsync(cancellationToken);
            var client = _containerClient.GetBlobClient(blobPath);
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            await client.UploadAsync(stream, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" }
            }, cancellationToken);

            _logger.LogInformation("Stored Blueprint JSON: {BlobPath}", blobPath);
            return blobPath;
        }

        await StoreLocalAsync(blobPath, System.Text.Encoding.UTF8.GetBytes(json), cancellationToken);
        return blobPath;
    }

    public async Task<string> StorePdfAsync(
        Guid blueprintId,
        int versionNumber,
        byte[] pdf,
        CancellationToken cancellationToken = default)
    {
        var blobPath = $"{blueprintId}/v{versionNumber}/blueprint.pdf";

        if (_containerClient != null)
        {
            await EnsureContainerExistsAsync(cancellationToken);
            var client = _containerClient.GetBlobClient(blobPath);
            using var stream = new MemoryStream(pdf);
            await client.UploadAsync(stream, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/pdf" }
            }, cancellationToken);

            _logger.LogInformation("Stored Blueprint PDF: {BlobPath}", blobPath);
            return blobPath;
        }

        await StoreLocalAsync(blobPath, pdf, cancellationToken);
        return blobPath;
    }

    public async Task<Stream?> GetJsonAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        if (_containerClient != null)
        {
            var client = _containerClient.GetBlobClient(blobPath);
            if (!await client.ExistsAsync(cancellationToken)) return null;
            var response = await client.DownloadStreamingAsync(cancellationToken: cancellationToken);
            return response.Value.Content;
        }

        return ReadLocal(blobPath);
    }

    public async Task<Stream?> GetPdfAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        if (_containerClient != null)
        {
            var client = _containerClient.GetBlobClient(blobPath);
            if (!await client.ExistsAsync(cancellationToken)) return null;
            var response = await client.DownloadStreamingAsync(cancellationToken: cancellationToken);
            return response.Value.Content;
        }

        return ReadLocal(blobPath);
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

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task EnsureContainerExistsAsync(CancellationToken ct) =>
        await _containerClient!.CreateIfNotExistsAsync(cancellationToken: ct);

    private async Task StoreLocalAsync(string blobPath, byte[] data, CancellationToken ct)
    {
        var localPath = LocalPath(blobPath);
        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
        await File.WriteAllBytesAsync(localPath, data, ct);
        _logger.LogInformation("Stored Blueprint artefact locally: {LocalPath}", localPath);
    }

    private Stream? ReadLocal(string blobPath)
    {
        var localPath = LocalPath(blobPath);
        return File.Exists(localPath) ? File.OpenRead(localPath) : null;
    }

    private string LocalPath(string blobPath) =>
        Path.Combine(_localBasePath, blobPath.Replace('/', Path.DirectorySeparatorChar));
}
