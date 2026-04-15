using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.Connectors;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Interfaces;

namespace StudioTechBI.Application.Services;

public class ConnectionService : IConnectionService
{
    private readonly IClientResolver _clientResolver;
    private readonly IDataConnectionRepository _connectionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ConnectionService> _logger;

    public ConnectionService(
        IClientResolver clientResolver,
        IDataConnectionRepository connectionRepository,
        IUnitOfWork unitOfWork,
        ILogger<ConnectionService> logger)
    {
        _clientResolver = clientResolver;
        _connectionRepository = connectionRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DataConnectionSummaryDto>> ListActiveConnectionsAsync(string clientIdOrCode, CancellationToken cancellationToken = default)
    {
        var client = await _clientResolver.ResolveAsync(clientIdOrCode, cancellationToken)
            ?? throw new InvalidOperationException("Client not found.");

        var list = await _connectionRepository.GetByClientIdAsync(client.Id, cancellationToken);
        return list.Select(c => new DataConnectionSummaryDto { Id = c.Id, Type = c.Type }).ToList();
    }

    public async Task<bool> DeactivateConnectionAsync(Guid connectionId, CancellationToken cancellationToken = default)
    {
        var entity = await _connectionRepository.GetByIdAsync(connectionId, cancellationToken);
        if (entity == null || entity.IsDeleted)
            return false;

        entity.IsDeleted = true;
        await _connectionRepository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deactivated data connection {ConnectionId} ({Type}) for client {ClientId}.", entity.Id, entity.Type, entity.ClientId);
        return true;
    }
}

