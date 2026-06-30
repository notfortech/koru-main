namespace StudioTechBI.Application.Interfaces;

/// <summary>
/// Stores and retrieves blueprint artefacts (JSON contracts, PDFs).
/// Backed by Azure Blob Storage in production; falls back to local filesystem when
/// AzureBlob:ConnectionString is absent (e.g. local development with UseDemoStorage).
/// Binary artefacts are never stored in SQL.
/// </summary>
public interface IBlueprintStorageService
{
    /// <summary>Persists the Analytics Deployment Contract JSON. Returns the relative blob path.</summary>
    Task<string> StoreJsonAsync(Guid blueprintId, int versionNumber, string json, CancellationToken cancellationToken = default);

    /// <summary>Persists the Blueprint PDF bytes. Returns the relative blob path.</summary>
    Task<string> StorePdfAsync(Guid blueprintId, int versionNumber, byte[] pdf, CancellationToken cancellationToken = default);

    /// <returns>A readable stream, or null if the blob does not exist.</returns>
    Task<Stream?> GetJsonAsync(string blobPath, CancellationToken cancellationToken = default);

    /// <returns>A readable stream, or null if the blob does not exist.</returns>
    Task<Stream?> GetPdfAsync(string blobPath, CancellationToken cancellationToken = default);

    Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default);
}
