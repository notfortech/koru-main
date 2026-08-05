using StudioTechBI.Application.DTOs.Credits;

namespace StudioTechBI.Application.Interfaces;

/// <summary>
/// Interim, locally-owned AI credit ledger (Client.AiCreditsRemaining) — real, DB-backed
/// enforcement while AgentHost's own plan-based ledger stays bypassed (see
/// CreditsOptions.BypassEnabled). Not a replacement for IAgentHostClient's credit contract —
/// callers that already check/consume against AgentHost keep doing so unchanged; this is an
/// additional, currently-authoritative gate layered in front of it.
/// </summary>
public interface ILocalCreditLedgerService
{
    Task<int> GetBalanceAsync(Guid clientId, CancellationToken cancellationToken = default);

    /// <summary>Read-only pre-flight check — never mutates the balance. Call before doing the
    /// actual AI-consuming work, so a client with zero credits never triggers a wasted AI call.</summary>
    Task<LocalCreditResult> CheckAsync(Guid clientId, int amount, CancellationToken cancellationToken = default);

    /// <summary>Deducts credits after the AI-consuming action has actually succeeded. Also checks
    /// whether the resulting balance crosses the low-balance threshold and, if so, auto-files a
    /// Pending, System-sourced CreditPurchaseRequest for admin visibility (deduplicated — never
    /// more than one open auto-filed request per client at a time).</summary>
    Task<LocalCreditResult> ConsumeAsync(Guid clientId, int amount, string feature, CancellationToken cancellationToken = default);

    /// <summary>Tops the balance back up — called when an admin marks a CreditPurchaseRequest
    /// paid. Returns the new balance.</summary>
    Task<int> GrantAsync(Guid clientId, int amount, string reason, CancellationToken cancellationToken = default);
}
