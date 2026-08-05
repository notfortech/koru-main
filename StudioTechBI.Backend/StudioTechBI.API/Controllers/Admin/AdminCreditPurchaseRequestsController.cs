using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.DTOs.Credits;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.API.Controllers.Admin;

/// <summary>
/// Staff-facing queue for "purchase AI credits" tickets. Fulfillment is deliberately manual — the
/// client's mock checkout only ever creates a Pending ticket; an admin confirms payment out of
/// band (bank transfer, invoice, whatever), then calls .../mark-paid here, which grants the
/// credits against AgentHost's ledger via IAgentHostClient.GrantCreditsAsync. Modeled directly on
/// AdminReportRequestsController's Pending/Fulfilled shape.
/// </summary>
[ApiController]
[Route("api/admin/credit-purchase-requests")]
[Authorize(Roles = "Admin,SuperAdmin,OperationsAdmin,SupportAdmin")]
public class AdminCreditPurchaseRequestsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAgentHostClient _agentHostClient;
    private readonly ILocalCreditLedgerService _localCredits;
    private readonly ILogger<AdminCreditPurchaseRequestsController> _logger;

    public AdminCreditPurchaseRequestsController(
        ApplicationDbContext db, IAgentHostClient agentHostClient, ILocalCreditLedgerService localCredits,
        ILogger<AdminCreditPurchaseRequestsController> logger)
    {
        _db = db;
        _agentHostClient = agentHostClient;
        _localCredits = localCredits;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllAsync([FromQuery] int? limit, CancellationToken cancellationToken)
    {
        var query = _db.CreditPurchaseRequests.OrderByDescending(r => r.CreatedAt).AsQueryable();
        if (limit is > 0)
            query = query.Take(limit.Value);

        var items = await query
            .Select(r => new CreditPurchaseRequestSummaryDto(
                r.Id, r.Status, r.CreditsRequested, r.PackLabel, r.CreatedAt, r.PaidAtUtc, r.Source))
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<List<CreditPurchaseRequestSummaryDto>>.SuccessResponse(items));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _db.CreditPurchaseRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity is null)
            return NotFound(ApiResponse<object>.ErrorResponse($"Credit purchase request {id} not found."));

        var dto = new CreditPurchaseRequestDetailDto(
            entity.Id, entity.ClientId, entity.Status, entity.RequestedByEmail, entity.CreditsRequested,
            entity.PackLabel, entity.Notes, entity.CreatedAt, entity.PaidAtUtc, entity.PaidByEmail, entity.Source);

        return Ok(ApiResponse<CreditPurchaseRequestDetailDto>.SuccessResponse(dto));
    }

    /// <summary>
    /// POST /api/admin/credit-purchase-requests/{id}/mark-paid
    /// Tops up the local interim ledger (see LocalCreditLedgerService) first — that's what
    /// actually gates AI-consuming actions today, so this is what makes credits genuinely "bounce
    /// back" for the client. Also best-effort mirrors the grant to AgentHost's own ledger for
    /// whenever that becomes the real source of truth; a failure there (e.g. AdminApiKey not
    /// configured, or the sidecar unreachable) is logged but never blocks the local top-up or the
    /// Paid status transition, unlike before this ledger existed.
    /// </summary>
    [HttpPost("{id:guid}/mark-paid")]
    public async Task<IActionResult> MarkPaidAsync(
        Guid id, [FromBody] MarkCreditPurchaseRequestPaidDto? body, CancellationToken cancellationToken)
    {
        var entity = await _db.CreditPurchaseRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity is null)
            return NotFound(ApiResponse<object>.ErrorResponse($"Credit purchase request {id} not found."));

        if (entity.Status == CreditPurchaseRequestStatuses.Paid)
            return BadRequest(ApiResponse<object>.ErrorResponse("This request has already been marked paid."));

        var reason = $"Credit purchase fulfilled — {entity.PackLabel}";
        var newLocalBalance = await _localCredits.GrantAsync(
            entity.ClientId, entity.CreditsRequested, reason, cancellationToken);

        var grant = await _agentHostClient.GrantCreditsAsync(
            entity.ClientId, entity.CreditsRequested, reason, entity.Id.ToString(), cancellationToken);
        if (grant is null)
        {
            _logger.LogWarning(
                "CreditPurchaseRequest.AgentHostGrantSkipped RequestId={RequestId} ClientId={ClientId} Credits={Credits} — local balance was still topped up.",
                id, entity.ClientId, entity.CreditsRequested);
        }

        entity.Status = CreditPurchaseRequestStatuses.Paid;
        entity.PaidAtUtc = DateTime.UtcNow;
        entity.PaidByEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(body?.Notes))
            entity.Notes = string.IsNullOrWhiteSpace(entity.Notes) ? body.Notes : $"{entity.Notes}\n{body.Notes}";

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "CreditPurchaseRequest.Paid RequestId={RequestId} ClientId={ClientId} CreditsGranted={CreditsGranted} LocalBalanceRemaining={LocalBalanceRemaining}",
            id, entity.ClientId, entity.CreditsRequested, newLocalBalance);

        return Ok(ApiResponse<object>.SuccessResponse(
            new { creditsGranted = entity.CreditsRequested, creditsRemaining = newLocalBalance },
            "Request marked paid — credits granted."));
    }
}
