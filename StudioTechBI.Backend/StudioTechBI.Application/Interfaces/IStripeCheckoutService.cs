namespace StudioTechBI.Application.Interfaces;

public record CheckoutSessionResult(string SessionId, string CheckoutUrl);

/// <summary>
/// Creates Stripe-hosted Checkout Sessions. Card data is entered on Stripe's own page and never
/// touches this backend — keeps this service entirely out of PCI scope. The caller supplies the
/// price (in cents) itself rather than trusting anything from the browser, so a tampered client
/// request can't change what gets charged.
/// </summary>
public interface IStripeCheckoutService
{
    Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
        long amountCents,
        string currency,
        string productName,
        string clientReferenceId,
        CancellationToken cancellationToken = default);

    /// <summary>Verifies the Stripe-Signature header against WebhookSecret and, if this is a
    /// "checkout.session.completed" event for a session that finished with payment actually
    /// collected, returns the session's ClientReferenceId (the same string passed into
    /// CreateCheckoutSessionAsync) so the caller can look up which request to fulfill.
    ///
    /// Returns null for: a bad/missing signature, a different event type, or a session that
    /// completed without payment (e.g. a $0 total) — callers should treat null as "200 OK,
    /// nothing to do here", matching Stripe's own guidance to acknowledge events it doesn't act
    /// on rather than erroring, so Stripe doesn't keep retrying delivery.
    ///
    /// Deliberately keeps every Stripe SDK type out of this interface's signature — only this
    /// service's Infrastructure implementation ever touches Stripe.Event directly.</summary>
    string? TryHandleCheckoutCompletedWebhook(string payloadJson, string stripeSignatureHeader);
}
