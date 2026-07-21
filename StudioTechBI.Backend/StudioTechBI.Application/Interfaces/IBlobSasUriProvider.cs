namespace StudioTechBI.Application.Interfaces;

/// <summary>
/// Generates short-lived, read-only SAS URIs for blobs in the "clients" container — closes the
/// Azure Blob/URL access gap flagged repeatedly this engagement (Power BI's cloud service needs
/// a URL it can fetch without interactive auth). Deliberately separate from IBlobStorageService/
/// BlobStorageService (not modified) — builds its own BlobServiceClient from the same
/// AzureBlob:ConnectionString config key, which already carries the account key SAS signing needs.
/// </summary>
public interface IBlobSasUriProvider
{
    /// <summary>Returns a read-only SAS URI valid for <paramref name="validFor"/>, or null if
    /// Azure Blob isn't configured or the client can't sign SAS tokens (e.g. no account key).</summary>
    Task<string?> GetReadSasUriAsync(string blobPath, TimeSpan validFor, CancellationToken cancellationToken = default);
}
