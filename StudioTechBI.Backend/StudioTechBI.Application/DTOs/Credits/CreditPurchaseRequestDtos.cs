namespace StudioTechBI.Application.DTOs.Credits;

/// <summary>Body for POST /api/credit-purchase-requests. Mock checkout — no real payment gateway;
/// the pack a client picks on PurchaseCreditsPage.</summary>
public record CreateCreditPurchaseRequestDto(int CreditsRequested, string PackLabel, string? Notes);

/// <summary>CheckoutUrl is null when Stripe isn't configured for this environment — the request
/// still files normally and falls back to the manual admin-fulfillment flow.</summary>
public record CreateCreditPurchaseRequestResponse(Guid RequestId, string? CheckoutUrl);

public record CreditPurchaseRequestSummaryDto(
    Guid RequestId,
    string Status,
    int CreditsRequested,
    string PackLabel,
    DateTime CreatedAt,
    DateTime? PaidAtUtc,
    string Source);

public record CreditPurchaseRequestDetailDto(
    Guid RequestId,
    Guid ClientId,
    string Status,
    string? RequestedByEmail,
    int CreditsRequested,
    string PackLabel,
    string? Notes,
    DateTime CreatedAt,
    DateTime? PaidAtUtc,
    string? PaidByEmail,
    string Source);

/// <summary>Body for AdminCreditPurchaseRequestsController's mark-paid action — no separate
/// "credits" field, since the amount granted is always exactly what the client requested.</summary>
public record MarkCreditPurchaseRequestPaidDto(string? Notes);
