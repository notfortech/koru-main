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
/// Client-facing "purchase AI credits" ticket. Creates a Stripe Checkout session for the request
/// when Stripe is configured (see StripeIntegrationExtensions, StripeWebhookController) — payment
/// then fulfils automatically via the webhook. Falls back to the original manual flow (see
/// AdminCreditPurchaseRequestsController.MarkPaidAsync: an admin confirms payment out of band and
/// marks the request paid) when Stripe isn't configured for this environment.
/// </summary>
[ApiController]
[Route("api/credit-purchase-requests")]
[Authorize]
public class CreditPurchaseRequestsController : ControllerBase
{
    // Server-trusted prices (cents) for each pack size — never trust a client-supplied amount.
    // Mirrors PurchaseCreditsPage.tsx's PACKS; keep both in sync if pricing changes.
    private static readonly Dictionary<int, long> PackPricesCents = new()
    {
        [100] = 1900,
        [500] = 7900,
        [1000] = 13900,
    };

    private readonly ApplicationDbContext _db;
    private readonly IClientService _clientService;
    private readonly IStripeCheckoutService _stripeCheckout;
    private readonly ILogger<CreditPurchaseRequestsController> _logger;

    public CreditPurchaseRequestsController(
        ApplicationDbContext db,
        IClientService clientService,
        IStripeCheckoutService stripeCheckout,
        ILogger<CreditPurchaseRequestsController> logger)
    {
        _db = db;
        _clientService = clientService;
        _stripeCheckout = stripeCheckout;
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

        if (!PackPricesCents.TryGetValue(request.CreditsRequested, out var amountCents))
            return BadRequest(ApiResponse<object>.ErrorResponse("Unrecognized credit pack."));

        var requestedByEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;

        var entity = new CreditPurchaseRequest
        {
            Id = Guid.NewGuid(),
            ClientId = client.ClientId,
            Status = CreditPurchaseRequestStatuses.Pending,
            Source = CreditPurchaseRequestSources.Client,
            RequestedByEmail = requestedByEmail,
            CreditsRequested = request.CreditsRequested,
            PackLabel = request.PackLabel,
            Notes = request.Notes,
        };

        string? checkoutUrl = null;
        try
        {
            var session = await _stripeCheckout.CreateCheckoutSessionAsync(
                amountCents, "usd", $"StudioTechBI AI credits — {request.PackLabel}",
                entity.Id.ToString(), cancellationToken);
            entity.StripeCheckoutSessionId = session.SessionId;
            checkoutUrl = session.CheckoutUrl;
        }
        catch (InvalidOperationException ex)
        {
            // Stripe isn't configured for this environment (blank SecretKey/AppBaseUrl) — file
            // the request as before and let the manual admin flow fulfil it.
            _logger.LogWarning(ex, "CreditPurchaseRequest.StripeNotConfigured — falling back to manual fulfillment.");
        }

        _db.CreditPurchaseRequests.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "CreditPurchaseRequest.Created RequestId={RequestId} ClientId={ClientId} Credits={Credits} CheckoutCreated={CheckoutCreated}",
            entity.Id, client.ClientId, entity.CreditsRequested, checkoutUrl is not null);

        var message = checkoutUrl is not null
            ? "Request submitted — continue to Stripe Checkout to complete payment."
            : "Request submitted — once payment is confirmed, your credits will be added automatically.";

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<CreateCreditPurchaseRequestResponse>.SuccessResponse(
                new CreateCreditPurchaseRequestResponse(entity.Id, checkoutUrl),
                message));
    }

    private async Task<ClientDto?> ResolveClientAsync(CancellationToken ct)
    {
        var clientCode = User.FindFirstValue("client_code")?.Trim();
        if (string.IsNullOrWhiteSpace(clientCode))
            return null;

        return await _clientService.GetByClientCodeOrIdAsync(clientCode, ct);
    }
}
