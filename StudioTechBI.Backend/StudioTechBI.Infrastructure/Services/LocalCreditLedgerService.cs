using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.Credits;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.Infrastructure.Services;

public class LocalCreditLedgerService : ILocalCreditLedgerService
{
    /// <summary>Below this, ConsumeAsync auto-files a CreditPurchaseRequest for admin
    /// visibility.</summary>
    public const int LowBalanceThreshold = 100;

    private const int AutoTopUpCreditsRequested = 500;
    private const string AutoTopUpPackLabel = "Auto top-up (low balance)";

    private readonly ApplicationDbContext _db;
    private readonly ILogger<LocalCreditLedgerService> _logger;

    public LocalCreditLedgerService(ApplicationDbContext db, ILogger<LocalCreditLedgerService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> GetBalanceAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        return await _db.Clients
            .Where(c => c.Id == clientId)
            .Select(c => (int?)c.AiCreditsRemaining)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;
    }

    public async Task<LocalCreditResult> CheckAsync(Guid clientId, int amount, CancellationToken cancellationToken = default)
    {
        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken);
        if (client is null)
            return new LocalCreditResult(false, 0, "Client not found.");

        if (client.AiCreditsRemaining < amount)
        {
            return new LocalCreditResult(
                false, client.AiCreditsRemaining,
                $"Insufficient AI credits ({client.AiCreditsRemaining} remaining, {amount} required). Consider topping up.");
        }

        return new LocalCreditResult(true, client.AiCreditsRemaining, null);
    }

    public async Task<LocalCreditResult> ConsumeAsync(
        Guid clientId, int amount, string feature, CancellationToken cancellationToken = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken);
        if (client is null)
            return new LocalCreditResult(false, 0, "Client not found.");

        // Clamped defensively rather than denied here -- CheckAsync is the gate; by the time we're
        // consuming, the AI call already happened and must be recorded, not refused after the fact.
        client.AiCreditsRemaining = Math.Max(0, client.AiCreditsRemaining - amount);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "AiCredits.Consumed ClientId={ClientId} Feature={Feature} Amount={Amount} Remaining={Remaining}",
            clientId, feature, amount, client.AiCreditsRemaining);

        if (client.AiCreditsRemaining < LowBalanceThreshold)
            await EnsureLowBalanceRequestFiledAsync(client, cancellationToken);

        return new LocalCreditResult(true, client.AiCreditsRemaining, null);
    }

    public async Task<int> GrantAsync(Guid clientId, int amount, string reason, CancellationToken cancellationToken = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken);
        if (client is null)
            throw new InvalidOperationException($"Client {clientId} not found.");

        client.AiCreditsRemaining += amount;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "AiCredits.Granted ClientId={ClientId} Amount={Amount} Reason={Reason} Remaining={Remaining}",
            clientId, amount, reason, client.AiCreditsRemaining);

        return client.AiCreditsRemaining;
    }

    // Auto-files into the same Pending queue a client's own "buy credits" click would -- reuses the
    // existing admin Credit Purchases page/Mark Paid action rather than inventing new admin UI.
    // Guarded so a client hovering just under the threshold across several actions doesn't spam
    // duplicate tickets: at most one open, System-sourced Pending request per client at a time.
    private async Task EnsureLowBalanceRequestFiledAsync(Client client, CancellationToken cancellationToken)
    {
        var alreadyFiled = await _db.CreditPurchaseRequests.AnyAsync(
            r => r.ClientId == client.Id
                && r.Source == CreditPurchaseRequestSources.System
                && r.Status == CreditPurchaseRequestStatuses.Pending,
            cancellationToken);
        if (alreadyFiled) return;

        _db.CreditPurchaseRequests.Add(new CreditPurchaseRequest
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            Status = CreditPurchaseRequestStatuses.Pending,
            Source = CreditPurchaseRequestSources.System,
            CreditsRequested = AutoTopUpCreditsRequested,
            PackLabel = AutoTopUpPackLabel,
            Notes = $"Auto-filed: AI credit balance dropped to {client.AiCreditsRemaining} " +
                    $"(below the {LowBalanceThreshold}-credit threshold).",
        });
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "CreditPurchaseRequest.AutoFiled ClientId={ClientId} BalanceRemaining={BalanceRemaining}",
            client.Id, client.AiCreditsRemaining);
    }
}
