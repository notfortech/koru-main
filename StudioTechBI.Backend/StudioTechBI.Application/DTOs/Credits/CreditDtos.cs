namespace StudioTechBI.Application.DTOs.Credits;

/// <summary>
/// Result of a pre-flight credit check against stbi-agenthost's shared credit ledger.
/// <see cref="Allowed"/> is false only when the tenant genuinely has no credits left
/// (agenthost returned 402) — any other failure (network, agenthost down) fails open so a
/// sidecar outage never blocks report generation on its own.
/// </summary>
public sealed record CreditCheckResult(
    bool Allowed,
    string? Plan,
    int? CreditsRemaining,
    bool IsUnlimited,
    DateTimeOffset? NextResetDate,
    string? DenialReason);

public sealed record CreditConsumeResult(
    int CreditsConsumed,
    int? CreditsRemaining,
    bool IsUnlimited,
    string? Plan,
    DateTimeOffset? ResetDate);

/// <summary>Result of an admin-fulfilled credit grant against AgentHost's ledger.</summary>
public sealed record CreditGrantResult(
    int CreditsGranted,
    int? CreditsRemaining,
    bool IsUnlimited,
    string? Plan,
    DateTimeOffset? ResetDate);

/// <summary>Aggregated usage for one tenant/feature — how Report Stats knows how many credits
/// were spent generating AI-assisted reports specifically.</summary>
public sealed record CreditUsageSummary(
    string Feature,
    int RequestCount,
    int TotalCreditsConsumed);

/// <summary>Result of a check or consume call against LocalCreditLedgerService's interim,
/// locally-owned balance (see Client.AiCreditsRemaining) — deliberately unaware of plans/resets,
/// unlike CreditCheckResult, since this is a flat per-client counter, not a subscription.
/// <see cref="Allowed"/> is false only when the balance genuinely can't cover the requested
/// amount (or the client can't be resolved) — this never fails open, unlike the AgentHost check,
/// since the local ledger has no external-outage failure mode to fail open against.</summary>
public sealed record LocalCreditResult(
    bool Allowed,
    int CreditsRemaining,
    string? DenialReason);
