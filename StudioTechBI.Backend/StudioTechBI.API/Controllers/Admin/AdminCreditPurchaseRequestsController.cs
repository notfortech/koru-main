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
/// Staff-facing queue for "purchase AI credits" tickets. Most requests now fulfil automatically
/// via Stripe's webhook (see StripeWebhookController), but a request can still be filed without a
/// Stripe checkout session when Stripe isn't configured — for those, an admin confirms payment out
/// of band (bank transfer, invoice, whatever), then calls .../mark-paid here. Both paths grant
/// credits through the same ICreditPurchaseFulfillmentService, so they behave identically.
/// Modeled directly on AdminReportRequestsController's Pending/Fulfilled shape.
/// </summary>
[ApiController]
[Route("api/admin/credit-purchase-requests")]
[Authorize(Roles = "Admin,SuperAdmin,OperationsAdmin,SupportAdmin")]
public class AdminCreditPurchaseRequestsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICreditPurchaseFulfillmentService _fulfillment;
    private readonly ILogger<AdminCreditPurchaseRequestsController> _logger;

    public AdminCreditPurchaseRequestsController(
        ApplicationDbContext db, ICreditPurchaseFulfillmentService fulfillment,
        ILogger<AdminCreditPurchaseRequestsController> logger)
    {
        _db = db;
        _fulfillment = fulfillment;
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
    /// Delegates the actual grant (local ledger top-up + best-effort AgentHost mirror + status
    /// transition) to ICreditPurchaseFulfillmentService — the same logic the Stripe webhook uses,
    /// so a manually-confirmed payment and an automatically-confirmed one land identically.
    /// </summary>
    [HttpPost("{id:guid}/mark-paid")]
    public async Task<IActionResult> MarkPaidAsync(
        Guid id, [FromBody] MarkCreditPurchaseRequestPaidDto? body, CancellationToken cancellationToken)
    {
        var exists = await _db.CreditPurchaseRequests.AnyAsync(r => r.Id == id, cancellationToken);
        if (!exists)
            return NotFound(ApiResponse<object>.ErrorResponse($"Credit purchase request {id} not found."));

        var paidByEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "unknown-admin";
        var result = await _fulfillment.FulfillAsync(id, paidByEmail, body?.Notes, cancellationToken);
        if (result is null)
            return BadRequest(ApiResponse<object>.ErrorResponse("This request has already been marked paid."));

        return Ok(ApiResponse<object>.SuccessResponse(
            new { creditsGranted = result.CreditsGranted, creditsRemaining = result.CreditsRemaining },
            "Request marked paid — credits granted."));
    }
}
