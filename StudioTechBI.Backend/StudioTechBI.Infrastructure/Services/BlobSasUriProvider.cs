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
}
