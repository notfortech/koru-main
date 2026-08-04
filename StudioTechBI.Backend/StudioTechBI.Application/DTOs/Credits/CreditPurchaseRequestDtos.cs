namespace StudioTechBI.Application.DTOs.Credits;

/// <summary>Body for POST /api/credit-purchase-requests. Mock checkout — no real payment gateway;
/// the pack a client picks on PurchaseCreditsPage.</summary>
public record CreateCreditPurchaseRequestDto(int CreditsRequested, string PackLabel, string? Notes);

public record CreateCreditPurchaseRequestResponse(Guid RequestId);

public record CreditPurchaseRequestSummaryDto(
    Guid RequestId,
    string Status,
    int CreditsRequested,
    string PackLabel,
    DateTime CreatedAt,
    DateTime? PaidAtUtc);

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
    string? PaidByEmail);

/// <summary>Body for AdminCreditPurchaseRequestsController's mark-paid action — no separate
/// "credits" field, since the amount granted is always exactly what the client requested.</summary>
public record MarkCreditPurchaseRequestPaidDto(string? Notes);
