using Microsoft.Graph;
using StudioTechBI.Application.DTOs.Connectors;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Connectors;

/// <summary>Placeholder: wire Microsoft Graph (Me.Drive) with delegated tokens.</summary>
public class OneDriveConnector : IDataConnector
{
    static OneDriveConnector() => _ = typeof(GraphServiceClient);

    public Task<IReadOnlyList<FileItem>> ListFilesAsync(DataConnection connection, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FileItem>>(Array.Empty<FileItem>());

    public Task<Stream> DownloadFileAsync(DataConnection connection, string fileId, CancellationToken cancellationToken = default) =>
        Task.FromException<Stream>(new NotSupportedException(
            "OneDrive connector is not implemented. Use Microsoft Graph with a delegated access token."));

    public Task<List<Dictionary<string, object>>> GetPreviewAsync(Stream fileStream, CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<Dictionary<string, object>>());
}
