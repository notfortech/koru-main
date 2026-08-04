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
    private readonly ILogger<AdminCreditPurchaseRequestsController> _logger;

    public AdminCreditPurchaseRequestsController(
        ApplicationDbContext db, IAgentHostClient agentHostClient, ILogger<AdminCreditPurchaseRequestsController> logger)
    {
        _db = db;
        _agentHostClient = agentHostClient;
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
                r.Id, r.Status, r.CreditsRequested, r.PackLabel, r.CreatedAt, r.PaidAtUtc))
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
            entity.PackLabel, entity.Notes, entity.CreatedAt, entity.PaidAtUtc, entity.PaidByEmail);

        return Ok(ApiResponse<CreditPurchaseRequestDetailDto>.SuccessResponse(dto));
    }

    /// <summary>
    /// POST /api/admin/credit-purchase-requests/{id}/mark-paid
    /// Grants the requested credits against AgentHost's ledger, then marks the ticket Paid — in
    /// that order, so a failed grant never leaves a ticket marked Paid with nothing actually
    /// granted.
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

        var grant = await _agentHostClient.GrantCreditsAsync(
            entity.ClientId, entity.CreditsRequested,
            $"Credit purchase fulfilled — {entity.PackLabel}", entity.Id.ToString(), cancellationToken);

        if (grant is null)
        {
            _logger.LogError(
                "CreditPurchaseRequest.GrantFailed RequestId={RequestId} ClientId={ClientId} Credits={Credits}",
                id, entity.ClientId, entity.CreditsRequested);
            return StatusCode(502, ApiResponse<object>.ErrorResponse(
                "Could not reach the credit ledger to grant these credits. Nothing was charged or changed — try again."));
        }

        entity.Status = CreditPurchaseRequestStatuses.Paid;
        entity.PaidAtUtc = DateTime.UtcNow;
        entity.PaidByEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(body?.Notes))
            entity.Notes = string.IsNullOrWhiteSpace(entity.Notes) ? body.Notes : $"{entity.Notes}\n{body.Notes}";

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "CreditPurchaseRequest.Paid RequestId={RequestId} ClientId={ClientId} CreditsGranted={CreditsGranted} CreditsRemaining={CreditsRemaining}",
            id, entity.ClientId, grant.CreditsGranted, grant.CreditsRemaining);

        return Ok(ApiResponse<object>.SuccessResponse(
            new { creditsGranted = grant.CreditsGranted, creditsRemaining = grant.CreditsRemaining },
            "Request marked paid — credits granted."));
    }
}
