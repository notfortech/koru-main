using StudioTechBI.Application.DTOs.Connectors;

namespace StudioTechBI.Application.Interfaces;

public interface IConnectionService
{
    Task<IReadOnlyList<DataConnectionSummaryDto>> ListActiveConnectionsAsync(string clientIdOrCode, CancellationToken cancellationToken = default);

    Task<bool> DeactivateConnectionAsync(Guid connectionId, CancellationToken cancellationToken = default);
}

