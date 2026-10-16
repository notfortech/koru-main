namespace StudioTechBI.Application.Models;

/// <summary>
/// Stripe Checkout configuration — blank by default (same convention as PowerBISettings/
/// ReportDesignerOptions), so the app boots fine with it unconfigured. Starts in test mode: set
/// SecretKey/PublishableKey to a Stripe TEST key pair (sk_test_.../pk_test_...) for the current
/// MVP phase; swap to live keys only when actually ready to charge real cards.
/// </summary>
public class StripeOptions
{
    public const string SectionName = "Stripe";

    /// <summary>Server-side secret key (sk_test_... / sk_live_...). Never logged, never returned
    /// to any client.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Publishable key (pk_test_... / pk_live_...) — safe to expose to the frontend if a
    /// future flow needs Stripe.js directly; unused by the Checkout-redirect flow today.</summary>
    public string PublishableKey { get; set; } = string.Empty;

    /// <summary>Signing secret for this endpoint's webhook (whsec_...), from the Stripe Dashboard
    /// or `stripe listen` in test mode. Required to verify that a /api/webhooks/stripe POST
    /// actually came from Stripe — see StripeWebhookController.</summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>Same convention as ReportValidationOptions.AppBaseUrl — the deployed frontend's
    /// own origin (e.g. "https://app.studiotechbi.com"), used to build Stripe Checkout's
    /// required success_url/cancel_url. Blank by default; must be set per-environment.</summary>
    public string AppBaseUrl { get; set; } = string.Empty;
}
