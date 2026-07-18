namespace StudioTechBI.Application.Models;

/// <summary>
/// Temporary override for AgentHost's shared AI credit ledger. AgentHost owns the real
/// plan-based balance/decrement/reset logic; while that side is being built out to the
/// 3000-credits/month plan, BypassEnabled short-circuits CheckCreditsAsync/ConsumeCreditAsync
/// in AgentHostClient so every tenant is always granted BypassCreditsRemaining credits without
/// calling out to AgentHost at all. Flip BypassEnabled back to false once AgentHost's real
/// ledger is ready.
/// </summary>
public class CreditsOptions
{
    public const string SectionName = "Credits";

    public bool BypassEnabled { get; set; }

    public int BypassCreditsRemaining { get; set; } = 1000;
}
