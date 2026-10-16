using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;
using Stripe;
using Stripe.Checkout;

namespace StudioTechBI.Infrastructure.Clients;

/// <summary>
/// Stripe Checkout integration. Every Checkout Session is created with ad-hoc `price_data`
/// (amount/currency/name given directly) rather than pre-created Stripe Price objects — simplest
/// path for the current fixed credit packs, no Stripe Dashboard setup required beyond the API
/// keys themselves. Revisit if/when real Stripe Products are wanted (e.g. for the subscription
/// plan tiers, a later phase).
/// </summary>
public class StripeCheckoutService : IStripeCheckoutService
{
    private readonly StripeOptions _opts;
    private readonly ILogger<StripeCheckoutService> _logger;

    public StripeCheckoutService(IOptions<StripeOptions> opts, ILogger<StripeCheckoutService> logger)
    {
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
        long amountCents, string currency, string productName, string clientReferenceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_opts.SecretKey))
            throw new InvalidOperationException(
                "Stripe:SecretKey is not configured. Set a test-mode secret key (sk_test_...) to enable checkout.");

        var baseUrl = string.IsNullOrWhiteSpace(_opts.AppBaseUrl)
            ? throw new InvalidOperationException("Stripe:AppBaseUrl is not configured.")
            : _opts.AppBaseUrl.TrimEnd('/');

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            ClientReferenceId = clientReferenceId,
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = currency,
                        UnitAmount = amountCents,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = productName,
                        },
                    },
                },
            },
            // CHECKOUT_SESSION_ID is a literal Stripe template token, substituted server-side by
            // Stripe itself when redirecting -- not interpolated by this code.
            SuccessUrl = $"{baseUrl}/client/purchase-credits?checkout=success&session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{baseUrl}/client/purchase-credits?checkout=cancelled",
        };

        var service = new SessionService(new StripeClient(_opts.SecretKey));
        var session = await service.CreateAsync(options, cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Stripe.CheckoutSessionCreated SessionId={SessionId} ClientReferenceId={ClientReferenceId} AmountCents={AmountCents} Currency={Currency}",
            session.Id, clientReferenceId, amountCents, currency);

        return new CheckoutSessionResult(session.Id, session.Url);
    }

    public string? TryHandleCheckoutCompletedWebhook(string payloadJson, string stripeSignatureHeader)
    {
        if (string.IsNullOrWhiteSpace(_opts.WebhookSecret))
        {
            _logger.LogError("Stripe.WebhookSecretNotConfigured — rejecting webhook, cannot verify signature.");
            return null;
        }

        Event stripeEvent;
        try
        {
            // Throws SignatureVerificationException on any mismatch -- never fall through and
            // trust the payload if this fails.
            stripeEvent = EventUtility.ConstructEvent(payloadJson, stripeSignatureHeader, _opts.WebhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe.WebhookSignatureInvalid — request rejected.");
            return null;
        }

        if (stripeEvent.Type != EventTypes.CheckoutSessionCompleted)
            return null;

        if (stripeEvent.Data.Object is not Session session)
            return null;

        // "paid" is Session.PaymentStatus's success value; a session can complete (e.g. for a
        // $0 total, or certain delayed payment methods) without this -- only act on an actual
        // successful payment.
        if (session.PaymentStatus != "paid")
        {
            _logger.LogInformation(
                "Stripe.CheckoutSessionCompletedWithoutPayment SessionId={SessionId} PaymentStatus={PaymentStatus}",
                session.Id, session.PaymentStatus);
            return null;
        }

        return session.ClientReferenceId;
    }
}
