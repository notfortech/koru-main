namespace StudioTechBI.Application.DTOs.Blueprint;

public sealed class BlueprintGenerateResponseDto
{
    public Guid RequestId { get; set; }

    /// <summary>Completed | PartiallyValid | Failed</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Secure URL to download the generated PDF — proxy through GET /api/blueprint/{requestId}/pdf.</summary>
    public string? PdfDownloadUrl { get; set; }

    public int CreditsConsumed { get; set; }
    public int CreditsRemaining { get; set; }
    public string? SubscriptionPlan { get; set; }
    public DateTimeOffset? ResetDate { get; set; }

    public List<string> Warnings { get; set; } = new();
}
