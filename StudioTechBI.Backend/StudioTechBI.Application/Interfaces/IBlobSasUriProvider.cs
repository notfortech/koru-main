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

    /// <summary>Returns a Write|Create-only SAS URI (no Read/Delete/List) valid for
    /// <paramref name="validFor"/>, or null if Azure Blob isn't configured or the client can't sign
    /// SAS tokens. Used for direct-to-blob browser uploads: the caller mints this for a specific,
    /// server-chosen blob path (never client-supplied) so the browser can PUT bytes straight to
    /// Blob Storage without the app tier ever buffering the file. Because the SAS itself carries no
    /// byte-count cap, the caller must verify the real committed size after upload via
    /// BlobClient.GetPropertiesAsync() before trusting the blob — this method only grants the
    /// ability to write, not a size guarantee.</summary>
    Task<string?> GetWriteSasUriAsync(string blobPath, TimeSpan validFor, CancellationToken cancellationToken = default);

    /// <summary>Returns the real, committed size of a blob (via a properties fetch, not a
    /// download), or null if it doesn't exist. This is the actual server-side enforcement point
    /// for the direct-to-blob upload flow's size limit: a write SAS carries no byte-count cap of
    /// its own, so the caller must check the real uploaded size after the fact, before trusting
    /// the blob or enqueueing any processing job against it.</summary>
    Task<long?> GetBlobSizeAsync(string blobPath, CancellationToken cancellationToken = default);

    /// <summary>Deletes a blob — used to clean up an over-limit or otherwise rejected direct
    /// upload immediately, rather than waiting on the container's lifecycle policy. A no-op
    /// (never throws) if the blob doesn't exist.</summary>
    Task DeleteBlobAsync(string blobPath, CancellationToken cancellationToken = default);
}
