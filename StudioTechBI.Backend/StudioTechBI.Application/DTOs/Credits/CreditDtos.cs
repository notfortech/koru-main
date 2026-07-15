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
