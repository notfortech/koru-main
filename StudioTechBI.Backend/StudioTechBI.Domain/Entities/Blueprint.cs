namespace StudioTechBI.Domain.Entities;

/// <summary>Persisted result of a Generate Blueprint call to the STBI AgentHost API.</summary>
public sealed class Blueprint : BaseEntity
{
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;

    public string BusinessRequirement { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;

    /// <summary>Optional JSON describing existing data schema sent to AgentHost.</summary>
    public string? ExistingSchema { get; set; }

    /// <summary>Completed | PartiallyValid | Failed | Pending</summary>
    public string Status { get; set; } = "Pending";

    /// <summary>Secure PDF download URL returned by AgentHost.</summary>
    public string? PdfDownloadUrl { get; set; }

    public int? CreditsConsumed { get; set; }
    public int? CreditsRemaining { get; set; }
    public string? SubscriptionPlan { get; set; }

    /// <summary>Date the credit pool resets (from AgentHost response).</summary>
    public DateTimeOffset? ResetDate { get; set; }

    /// <summary>Blob path where the full AgentHost JSON response is stored for AI training.</summary>
    public string? ResponseBlobPath { get; set; }

    /// <summary>Serialized JSON array of warnings from AgentHost.</summary>
    public string? WarningsJson { get; set; }

    public string? RequestedByEmail { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
