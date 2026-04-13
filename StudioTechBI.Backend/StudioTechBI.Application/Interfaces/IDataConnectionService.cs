using StudioTechBI.Application.DTOs.Connectors;

namespace StudioTechBI.Application.Interfaces;

public interface IDataConnectionService
{
    Task<IReadOnlyList<FileItem>> ListFilesAsync(Guid connectionId, CancellationToken cancellationToken = default);

    /// <summary>Downloads once from connector, uploads to accounting/created/, returns blob path. Reuses path if same fileId already imported.</summary>
    Task<string> ImportFileToCreatedBlobAsync(Guid connectionId, string fileId, string? preferredFileName, CancellationToken cancellationToken = default);

    Task<DataConnectionDto> RegisterConnectionAsync(RegisterDataConnectionDto dto, CancellationToken cancellationToken = default);
}
