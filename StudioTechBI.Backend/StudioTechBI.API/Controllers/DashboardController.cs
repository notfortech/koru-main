using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.Application.DTOs.Dashboard;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Domain.Interfaces;

namespace StudioTechBI.API.Controllers;

/// <summary>Pre-aggregated KPIs and chart data for client portal (accountants vs end clients). Admin portal JWTs are rejected.</summary>
[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = "Client,Accountant,Admin")]
public class DashboardController : ControllerBase
{
    private readonly IClientPortalDashboardService _dashboard;
    private readonly IClientByCompanyQuery _clientByCompany;
    private readonly IClientService _clientService;
    private readonly IRepository<User> _userRepository;

    public DashboardController(
        IClientPortalDashboardService dashboard,
        IClientByCompanyQuery clientByCompany,
        IClientService clientService,
        IRepository<User> userRepository)
    {
        _dashboard = dashboard;
        _clientByCompany = clientByCompany;
        _clientService = clientService;
        _userRepository = userRepository;
    }

    /// <summary>
    /// Returns aggregated dashboard payload (no transactions, account numbers, or tax IDs).
    /// Accountants may pass <paramref name="clientId"/> to scope to one client when they have access to several.
    /// </summary>
    /// <param name="clientId">Optional; accountant-only filter for a single client.</param>
    /// <param name="months">Chart window length (default 6, max 12).</param>
    [HttpGet]
    [ProducesResponseType(typeof(ClientDashboardResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ClientDashboardResponseDto>> Get(
        [FromQuery] Guid? clientId,
        [FromQuery] int months = 6,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(User.FindFirstValue("AdminId")))
            return Forbid();

        var roles = User.FindAll(ClaimTypes.Role).Select(r => r.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var isAccountantView = roles.Contains("Accountant") || roles.Contains("Admin");
        var isClientView = roles.Contains("Client") && !isAccountantView;
        if (!isAccountantView && !isClientView)
            return Forbid();

        var email = User.FindFirstValue(ClaimTypes.Email);
        List<Guid> scopedIds;

        if (isAccountantView)
        {
            scopedIds = await GetAccessibleClientIdsForAccountantAsync(email, cancellationToken);
            if (clientId.HasValue)
            {
                if (!scopedIds.Contains(clientId.Value))
                    return Forbid();
                scopedIds = new List<Guid> { clientId.Value };
            }
        }
        else
        {
            var single = await ResolveSingleClientIdForClientUserAsync(email, cancellationToken);
            scopedIds = single.HasValue ? new List<Guid> { single.Value } : new List<Guid>();
        }

        var context = new DashboardRequestContext
        {
            Email = email,
            IsAccountantView = isAccountantView,
            IsClientView = isClientView,
            ScopedClientIds = scopedIds
        };

        var dto = await _dashboard.GetDashboardAsync(context, months, cancellationToken);
        return Ok(dto);
    }

    private async Task<List<Guid>> GetAccessibleClientIdsForAccountantAsync(string? email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
            return new List<Guid>();

        var fromCompanies = await _clientByCompany.GetClientsForUserEmailAsync(email.Trim(), cancellationToken);
        var ids = fromCompanies.Select(c => c.ClientId).Distinct().ToList();

        if (ids.Count > 0)
            return ids;

        var claimCode = User.FindFirstValue("client_code")?.Trim();
        if (string.IsNullOrEmpty(claimCode))
            return ids;

        var client = await _clientService.GetByClientCodeOrIdAsync(claimCode, cancellationToken);
        if (client != null)
            ids.Add(client.ClientId);
        return ids;
    }

    private async Task<Guid?> ResolveSingleClientIdForClientUserAsync(string? email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var normalized = email.Trim().ToLowerInvariant();
        var fromCompany = await _clientByCompany.GetClientForUserEmailAsync(normalized, cancellationToken);
        if (fromCompany != null)
            return fromCompany.ClientId;

        var user = await _userRepository.GetByPredicateAsync(
            u => u.Email == normalized && !u.IsDeleted,
            cancellationToken);
        return user?.ClientId;
    }
}
