using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.API.Controllers;

/// <summary>
/// Receives Stripe's "checkout.session.completed" webhook and fulfils the matching
/// CreditPurchaseRequest — the automatic counterpart to
/// AdminCreditPurchaseRequestsController.MarkPaidAsync's manual flow. Always acknowledges with
/// 200 (per Stripe's own guidance: ack events this endpoint doesn't act on rather than erroring,
/// so Stripe doesn't keep retrying delivery) — the only thing that varies is whether a credit
/// grant actually happened.
/// </summary>
[ApiController]
[Route("api/webhooks/stripe")]
[AllowAnonymous]
public class StripeWebhookController : ControllerBase
{
    private readonly IStripeCheckoutService _stripeCheckout;
    private readonly ICreditPurchaseFulfillmentService _fulfillment;
    private readonly ILogger<StripeWebhookController> _logger;

    public StripeWebhookController(
        IStripeCheckoutService stripeCheckout, ICreditPurchaseFulfillmentService fulfillment,
        ILogger<StripeWebhookController> logger)
    {
        _stripeCheckout = stripeCheckout;
        _fulfillment = fulfillment;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> HandleAsync(CancellationToken cancellationToken)
    {
        string payload;
        using (var reader = new StreamReader(Request.Body))
        {
            payload = await reader.ReadToEndAsync(cancellationToken);
        }

        var signatureHeader = Request.Headers["Stripe-Signature"].ToString();
        var clientReferenceId = _stripeCheckout.TryHandleCheckoutCompletedWebhook(payload, signatureHeader);
        if (clientReferenceId is null)
        {
            // Bad signature, an event type this endpoint doesn't act on, or a session that
            // completed without payment -- none of these are errors from Stripe's point of view.
            return Ok();
        }

        if (!Guid.TryParse(clientReferenceId, out var requestId))
        {
            _logger.LogWarning(
                "StripeWebhook.UnparseableClientReferenceId ClientReferenceId={ClientReferenceId}",
                clientReferenceId);
            return Ok();
        }

        var result = await _fulfillment.FulfillAsync(requestId, "stripe-webhook", null, cancellationToken);
        if (result is null)
        {
            // Not found, or already Paid -- Stripe may legitimately redeliver this event, so a
            // second delivery landing here as a no-op is expected, not an error.
            _logger.LogInformation(
                "StripeWebhook.FulfillmentSkipped RequestId={RequestId} — not found or already paid.",
                requestId);
            return Ok();
        }

        _logger.LogInformation(
            "StripeWebhook.Fulfilled RequestId={RequestId} CreditsGranted={CreditsGranted}",
            requestId, result.CreditsGranted);

        return Ok();
    }
}
