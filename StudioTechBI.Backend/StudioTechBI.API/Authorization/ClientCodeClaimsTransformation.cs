using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.Interfaces;
using System.Security.Claims;

namespace StudioTechBI.API.Authorization;

/// <summary>
/// Adds the client_code claim from the database when the JWT doesn't have it.
/// Fixes "folder mapped but report says not linked" when the user was assigned to a client after logging in (stale token).
/// </summary>
public class ClientCodeClaimsTransformation : IClaimsTransformation
{
    private readonly IClientService _clientService;
    private readonly ILogger<ClientCodeClaimsTransformation> _logger;

    public ClientCodeClaimsTransformation(IClientService clientService, ILogger<ClientCodeClaimsTransformation> logger)
    {
        _clientService = clientService;
        _logger = logger;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal?.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
            return principal;

        if (principal.HasClaim(c => c.Type == "client_code"))
            return principal;

        var email = principal.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrWhiteSpace(email))
            return principal;

        try
        {
            var clientCode = await _clientService.GetClientCodeForUserEmailAsync(email.Trim(), default);
            if (!string.IsNullOrEmpty(clientCode))
            {
                identity.AddClaim(new Claim("client_code", clientCode));
                _logger.LogDebug("Added client_code claim '{ClientCode}' for user {Email} (token was missing it).", clientCode, email);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load client_code for user {Email} during claims transformation.", email);
        }

        return principal;
    }
}
