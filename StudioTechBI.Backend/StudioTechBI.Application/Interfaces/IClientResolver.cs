using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Application.Interfaces;

public interface IClientResolver
{
    Task<Client?> ResolveAsync(string clientIdOrCode, CancellationToken cancellationToken = default);
}

