using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.DTOs.Credits;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.API.Controllers;

/// <summary>
/// Client-facing "purchase AI credits" ticket — a mock checkout for now (see
/// PurchaseCreditsPage.tsx: no real payment gateway). Fulfillment is manual (see
/// AdminCreditPurchaseRequestsController): an admin confirms payment out of band, then marks the
/// request paid, which grants the credits against AgentHost's ledger. This controller only ever
/// creates the ticket.
/// </summary>
[ApiController]
[Route("api/credit-purchase-requests")]
[Authorize]
public class CreditPurchaseRequestsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IClientService _clientService;
    private readonly ILogger<CreditPurchaseRequestsController> _logger;

    public CreditPurchaseRequestsController(ApplicationDbContext db, IClientService clientService, ILogger<CreditPurchaseRequestsController> logger)
    {
        _db = db;
        _clientService = clientService;
        _logger = logger;
    }

    /// <summary>POST /api/credit-purchase-requests</summary>
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateCreditPurchaseRequestDto request, CancellationToken cancellationToken)
    {
        var client = await ResolveClientAsync(cancellationToken);
        if (client is null)
            return BadRequest(ApiResponse<object>.ErrorResponse("A resolvable client_code claim is required."));

        if (request is null || request.CreditsRequested <= 0 || string.IsNullOrWhiteSpace(request.PackLabel))
            return BadRequest(ApiResponse<object>.ErrorResponse("A valid credit pack must be selected."));

        var requestedByEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;

        var entity = new CreditPurchaseRequest
        {
            Id = Guid.NewGuid(),
            ClientId = client.ClientId,
            Status = CreditPurchaseRequestStatuses.Pending,
            RequestedByEmail = requestedByEmail,
            CreditsRequested = request.CreditsRequested,
            PackLabel = request.PackLabel,
            Notes = request.Notes,
        };

        _db.CreditPurchaseRequests.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "CreditPurchaseRequest.Created RequestId={RequestId} ClientId={ClientId} Credits={Credits}",
            entity.Id, client.ClientId, entity.CreditsRequested);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<CreateCreditPurchaseRequestResponse>.SuccessResponse(
                new CreateCreditPurchaseRequestResponse(entity.Id),
                "Request submitted — once payment is confirmed, your credits will be added automatically."));
    }

    private async Task<ClientDto?> ResolveClientAsync(CancellationToken ct)
    {
        var clientCode = User.FindFirstValue("client_code")?.Trim();
        if (string.IsNullOrWhiteSpace(clientCode))
            return null;

        return await _clientService.GetByClientCodeOrIdAsync(clientCode, ct);
    }
}
