namespace StudioTechBI.Application.Interfaces;

public interface IBlobStorageService
{
    Task CreateClientFolderStructureAsync(string clientId, CancellationToken cancellationToken = default);
    Task<string> UploadTemplateAsync(string templateName, string industry, string version, Stream content, string fileName, CancellationToken cancellationToken = default);
    Task<Stream?> DownloadBlobAsync(string path, CancellationToken cancellationToken = default);
    /// <summary>Checks whether a blob exists (no download).</summary>
    Task<bool> BlobExistsAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the full blob name/path of the newest blob under <paramref name="pathPrefix"/> matching <paramref name="fileExtension"/> (e.g. ".xlsx"), or null if none.
    /// </summary>
    Task<string?> GetLatestBlobPathByPrefixAsync(string pathPrefix, string fileExtension, CancellationToken cancellationToken = default);

    /// <summary>Uploads a blob into the clients container at the given path (overwrite).</summary>
    Task UploadClientBlobAsync(string blobPath, Stream content, string? contentType = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes a blob if it exists; a no-op (never throws) if it doesn't.</summary>
    Task DeleteBlobIfExistsAsync(string blobPath, CancellationToken cancellationToken = default);
}
