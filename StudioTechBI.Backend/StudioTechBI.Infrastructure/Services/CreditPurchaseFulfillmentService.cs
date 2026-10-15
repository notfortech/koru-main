using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.Infrastructure.Services;

/// <summary>
/// Extracted from AdminCreditPurchaseRequestsController.MarkPaidAsync so the admin's manual
/// "mark paid" action and the Stripe webhook's automatic fulfillment grant credits through
/// exactly the same sequence (local ledger top-up + best-effort AgentHost mirror + status
/// transition), rather than each reimplementing it.
/// </summary>
public class CreditPurchaseFulfillmentService : ICreditPurchaseFulfillmentService
{
    private readonly ApplicationDbContext _db;
    private readonly IAgentHostClient _agentHostClient;
    private readonly ILocalCreditLedgerService _localCredits;
    private readonly ILogger<CreditPurchaseFulfillmentService> _logger;

    public CreditPurchaseFulfillmentService(
        ApplicationDbContext db,
        IAgentHostClient agentHostClient,
        ILocalCreditLedgerService localCredits,
        ILogger<CreditPurchaseFulfillmentService> logger)
    {
        _db = db;
        _agentHostClient = agentHostClient;
        _localCredits = localCredits;
        _logger = logger;
    }

    public async Task<CreditPurchaseFulfillmentResult?> FulfillAsync(
        Guid requestId, string paidByEmail, string? notes, CancellationToken cancellationToken = default)
    {
        var entity = await _db.CreditPurchaseRequests.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (entity is null)
        {
            _logger.LogWarning("CreditPurchaseFulfillment.NotFound RequestId={RequestId}", requestId);
            return null;
        }

        if (entity.Status == CreditPurchaseRequestStatuses.Paid)
        {
            // Already fulfilled -- most relevant for the webhook path, which Stripe may retry
            // delivery of. Granting a second time would double the client's credits for one
            // purchase, so this is a deliberate no-op, not an error.
            _logger.LogInformation(
                "CreditPurchaseFulfillment.AlreadyPaid RequestId={RequestId} ClientId={ClientId}",
                requestId, entity.ClientId);
            return null;
        }

        var reason = $"Credit purchase fulfilled — {entity.PackLabel}";
        var newLocalBalance = await _localCredits.GrantAsync(entity.ClientId, entity.CreditsRequested, reason, cancellationToken);

        var grant = await _agentHostClient.GrantCreditsAsync(
            entity.ClientId, entity.CreditsRequested, reason, entity.Id.ToString(), cancellationToken);
        if (grant is null)
        {
            _logger.LogWarning(
                "CreditPurchaseFulfillment.AgentHostGrantSkipped RequestId={RequestId} ClientId={ClientId} Credits={Credits} — local balance was still topped up.",
                requestId, entity.ClientId, entity.CreditsRequested);
        }

        entity.Status = CreditPurchaseRequestStatuses.Paid;
        entity.PaidAtUtc = DateTime.UtcNow;
        entity.PaidByEmail = paidByEmail;
        if (!string.IsNullOrWhiteSpace(notes))
            entity.Notes = string.IsNullOrWhiteSpace(entity.Notes) ? notes : $"{entity.Notes}\n{notes}";

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "CreditPurchaseFulfillment.Paid RequestId={RequestId} ClientId={ClientId} CreditsGranted={CreditsGranted} LocalBalanceRemaining={LocalBalanceRemaining} PaidByEmail={PaidByEmail}",
            requestId, entity.ClientId, entity.CreditsRequested, newLocalBalance, paidByEmail);

        return new CreditPurchaseFulfillmentResult(entity.CreditsRequested, newLocalBalance);
    }
}
