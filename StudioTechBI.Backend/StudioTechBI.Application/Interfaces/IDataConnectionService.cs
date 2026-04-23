using StudioTechBI.Application.DTOs.Connectors;

namespace StudioTechBI.Application.Interfaces;

public interface IDataConnectionService
{
    Task<IReadOnlyList<DataConnectionSummaryDto>> ListConnectionsForClientAsync(Guid clientId, CancellationToken cancellationToken = default);

    /// <summary>Returns the client id for a connection, or null if missing.</summary>
    Task<Guid?> GetConnectionClientIdAsync(Guid connectionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FileItem>> ListFilesAsync(Guid connectionId, CancellationToken cancellationToken = default);

    /// <summary>Downloads once from connector, uploads to accounting/created/, returns blob path. Reuses path if same fileId already imported.</summary>
    Task<string> ImportFileToCreatedBlobAsync(Guid connectionId, string fileId, string? preferredFileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports the file to Blob, generates a small sample for AI, requests model suggestions, persists them, and returns them.
    /// </summary>
    Task<ConnectorImportResponseDto> ImportFileAndRecommendModelsAsync(Guid connectionId, string fileId, string? preferredFileName, CancellationToken cancellationToken = default);

    Task<DataConnectionDto> RegisterConnectionAsync(RegisterDataConnectionDto dto, CancellationToken cancellationToken = default);
}
