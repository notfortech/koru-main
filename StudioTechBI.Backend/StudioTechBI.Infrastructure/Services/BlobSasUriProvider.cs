using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.Infrastructure.Services;

public class BlobSasUriProvider : IBlobSasUriProvider
{
    private const string ContainerName = "clients";
    private readonly BlobContainerClient? _containerClient;
    private readonly ILogger<BlobSasUriProvider> _logger;

    public BlobSasUriProvider(IConfiguration configuration, ILogger<BlobSasUriProvider> logger)
    {
        _logger = logger;
        var connectionString = configuration["AzureBlob:ConnectionString"];
        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogWarning("Azure Blob connection string not configured. SAS URI generation will no-op.");
            _containerClient = null;
            return;
        }
        var blobServiceClient = new BlobServiceClient(connectionString);
        _containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
    }

    public Task<string?> GetReadSasUriAsync(string blobPath, TimeSpan validFor, CancellationToken cancellationToken = default)
    {
        if (_containerClient == null)
            return Task.FromResult<string?>(null);

        var client = _containerClient.GetBlobClient(blobPath);
        if (!client.CanGenerateSasUri)
        {
            _logger.LogWarning("Blob client cannot generate SAS URIs (no account key credential) for {Path}", blobPath);
            return Task.FromResult<string?>(null);
        }

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = ContainerName,
            BlobName = blobPath,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(validFor),
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sasUri = client.GenerateSasUri(sasBuilder);
        return Task.FromResult<string?>(sasUri.ToString());
    }

    public Task<string?> GetWriteSasUriAsync(string blobPath, TimeSpan validFor, CancellationToken cancellationToken = default)
    {
        if (_containerClient == null)
            return Task.FromResult<string?>(null);

        var client = _containerClient.GetBlobClient(blobPath);
        if (!client.CanGenerateSasUri)
        {
            _logger.LogWarning("Blob client cannot generate SAS URIs (no account key credential) for {Path}", blobPath);
            return Task.FromResult<string?>(null);
        }

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = ContainerName,
            BlobName = blobPath,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(validFor),
        };
        // Write + Create only -- deliberately no Read/Delete/List, so a leaked URL can only ever
        // be used to upload to this one specific blob path, never read it back or discover others.
        sasBuilder.SetPermissions(BlobSasPermissions.Write | BlobSasPermissions.Create);

        var sasUri = client.GenerateSasUri(sasBuilder);
        return Task.FromResult<string?>(sasUri.ToString());
    }

    public async Task<long?> GetBlobSizeAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        if (_containerClient == null)
            return null;

        var client = _containerClient.GetBlobClient(blobPath);
        try
        {
            var properties = await client.GetPropertiesAsync(cancellationToken: cancellationToken);
            return properties.Value.ContentLength;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteBlobAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        if (_containerClient == null)
            return;

        var client = _containerClient.GetBlobClient(blobPath);
        try
        {
            await client.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete blob at {Path} — non-fatal.", blobPath);
        }
    }
}
