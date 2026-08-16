using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.Infrastructure.Services;

public class BlobStorageService : IBlobStorageService
{
    private const string ContainerName = "clients";
    private readonly Azure.Storage.Blobs.BlobContainerClient? _containerClient;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(IConfiguration configuration, ILogger<BlobStorageService> logger)
    {
        _logger = logger;
        var connectionString = configuration["AzureBlob:ConnectionString"];
        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogWarning("Azure Blob connection string not configured. Blob operations will no-op.");
            _containerClient = null;
            return;
        }
        var blobServiceClient = new Azure.Storage.Blobs.BlobServiceClient(connectionString);
        _containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
    }

    public async Task CreateClientFolderStructureAsync(string clientId, CancellationToken cancellationToken = default)
    {
        if (_containerClient == null) return;
        try
        {
            await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            var rootFolders = new[] { "uploads", "validated", "errors", "master" };
            var accountingFolders = new[] { "uploads", "created", "validated" };
            var basePath = $"{clientId}/";
            foreach (var folder in rootFolders)
            {
                var path = $"{basePath}{folder}/";
                var blob = _containerClient.GetBlobClient(path + ".keep");
                await blob.UploadAsync(
                    new BinaryData(Array.Empty<byte>()),
                    overwrite: true,
                    cancellationToken);
            }

            foreach (var folder in accountingFolders)
            {
                var path = $"{basePath}accounting/{folder}/";
                var blob = _containerClient.GetBlobClient(path + ".keep");
                await blob.UploadAsync(
                    new BinaryData(Array.Empty<byte>()),
                    overwrite: true,
                    cancellationToken);
            }
            _logger.LogInformation("Created blob folder structure for client {ClientId}", clientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create blob folder structure for client {ClientId}", clientId);
            throw;
        }
    }

    public async Task<string> UploadTemplateAsync(string templateName, string industry, string version, Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        if (_containerClient == null)
            return $"templates/{industry}/{version}/{fileName}";
        await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var path = $"templates/{industry}/{version}/{fileName}";
        var client = _containerClient.GetBlobClient(path);
        await client.UploadAsync(content, overwrite: true, cancellationToken);
        _logger.LogInformation("Uploaded template to {Path}", path);
        return path;
    }

    public async Task<Stream?> DownloadBlobAsync(string path, CancellationToken cancellationToken = default)
    {
        if (_containerClient == null) return null;
        var client = _containerClient.GetBlobClient(path);
        if (!await client.ExistsAsync(cancellationToken))
            return null;
        var response = await client.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return response.Value.Content;
    }

    public async Task<bool> BlobExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        if (_containerClient == null) return false;
        var client = _containerClient.GetBlobClient(path);
        return await client.ExistsAsync(cancellationToken);
    }

    public async Task DeleteBlobIfExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        if (_containerClient == null) return;
        var client = _containerClient.GetBlobClient(path);
        await client.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    public async Task<string?> GetLatestBlobPathByPrefixAsync(string pathPrefix, string fileExtension, CancellationToken cancellationToken = default)
    {
        if (_containerClient == null) return null;

        var prefix = (pathPrefix ?? "").Trim().Replace("\\", "/");
        if (prefix.Length == 0)
            return null;

        var ext = (fileExtension ?? "").Trim();
        if (!ext.StartsWith(".", StringComparison.Ordinal))
            ext = "." + ext;

        var latestPath = (string?)null;
        DateTimeOffset? latestModified = null;

        await foreach (var item in _containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
        {
            var name = item.Name;
            if (!name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                continue;

            var modified = item.Properties.LastModified;
            if (!modified.HasValue)
                continue;

            if (latestModified == null || modified > latestModified)
            {
                latestModified = modified.Value;
                latestPath = name;
            }
        }

        if (latestPath != null)
            _logger.LogDebug("Latest blob under prefix {Prefix}: {Path}", prefix, latestPath);

        return latestPath;
    }

    public async Task UploadClientBlobAsync(string blobPath, Stream content, string? contentType = null, CancellationToken cancellationToken = default)
    {
        if (_containerClient == null)
            throw new InvalidOperationException("Azure Blob is not configured (AzureBlob:ConnectionString).");

        await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var path = (blobPath ?? "").Trim().Replace("\\", "/");
        if (path.Length == 0)
            throw new ArgumentException("Blob path is required.", nameof(blobPath));

        var client = _containerClient.GetBlobClient(path);
        var headers = new Azure.Storage.Blobs.Models.BlobHttpHeaders();
        if (!string.IsNullOrWhiteSpace(contentType))
            headers.ContentType = contentType;

        await client.UploadAsync(content, new Azure.Storage.Blobs.Models.BlobUploadOptions { HttpHeaders = headers }, cancellationToken);
        _logger.LogInformation("Uploaded client blob to {Path}", path);
    }
}
