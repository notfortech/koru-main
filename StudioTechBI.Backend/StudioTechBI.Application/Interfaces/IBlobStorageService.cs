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
}
