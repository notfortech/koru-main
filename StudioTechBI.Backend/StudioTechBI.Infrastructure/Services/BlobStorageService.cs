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
            var folders = new[] { "uploads", "validated", "errors", "master" };
            var basePath = $"{clientId}/";
            foreach (var folder in folders)
            {
                var path = $"{basePath}{folder}/";
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
}
