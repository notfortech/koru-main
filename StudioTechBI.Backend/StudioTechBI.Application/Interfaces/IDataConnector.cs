using StudioTechBI.Application.DTOs.Connectors;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Application.Interfaces;

public interface IDataConnector
{
    Task<IReadOnlyList<FileItem>> ListFilesAsync(DataConnection connection, CancellationToken cancellationToken = default);

    Task<Stream> DownloadFileAsync(DataConnection connection, string fileId, CancellationToken cancellationToken = default);

    Task<List<Dictionary<string, object>>> GetPreviewAsync(Stream fileStream, CancellationToken cancellationToken = default);
}
