using System.Security.Claims;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.Infrastructure.Services;

/// <inheritdoc cref="IClientAccessGuard"/>
public sealed class ClientAccessGuard : IClientAccessGuard
{
    private readonly IClientByCompanyQuery _clientByCompanyQuery;
    private readonly IClientService _clientService;

    public ClientAccessGuard(IClientByCompanyQuery clientByCompanyQuery, IClientService clientService)
    {
        _clientByCompanyQuery = clientByCompanyQuery;
        _clientService = clientService;
    }

    public async Task<bool> CanAccessClientAsync(ClaimsPrincipal user, Guid clientId, CancellationToken cancellationToken = default)
    {
        var email = user.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email)) return false;

        var fromCompanies = await _clientByCompanyQuery.GetClientsForUserEmailAsync(email, cancellationToken);
        if (fromCompanies.Any(c => c.ClientId == clientId))
            return true;

        var claimCode = user.FindFirst("client_code")?.Value;
        if (string.IsNullOrEmpty(claimCode)) return false;

        var fromClaim = await _clientService.GetByClientCodeOrIdAsync(claimCode, cancellationToken);
        return fromClaim != null && fromClaim.ClientId == clientId;
    }
}
