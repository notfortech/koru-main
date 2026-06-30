namespace StudioTechBI.Application.DTOs.Blueprint;

public sealed class BlueprintCreditsDto
{
    public int CreditsRemaining { get; set; }
    public int CreditsConsumed { get; set; }
    public string? SubscriptionPlan { get; set; }
    public DateTimeOffset? ResetDate { get; set; }
}
