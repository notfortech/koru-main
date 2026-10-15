namespace StudioTechBI.Application.Interfaces;

public record CreditPurchaseFulfillmentResult(int CreditsGranted, int CreditsRemaining);

/// <summary>
/// The actual "grant the credits" logic for a CreditPurchaseRequest — shared between the admin's
/// manual mark-paid action and the Stripe webhook's automatic fulfillment, so both paths grant
/// credits identically rather than duplicating the local-ledger-top-up + AgentHost-grant +
/// status-transition sequence in two places.
/// </summary>
public interface ICreditPurchaseFulfillmentService
{
    /// <summary>Idempotent-ish: returns null (no-op) if the request is already Paid, rather than
    /// granting credits a second time — matters most for the webhook path, which Stripe may
    /// retry delivery of.</summary>
    Task<CreditPurchaseFulfillmentResult?> FulfillAsync(
        Guid requestId,
        string paidByEmail,
        string? notes,
        CancellationToken cancellationToken = default);
}
