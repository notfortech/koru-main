namespace StudioTechBI.Domain.Entities;

/// <summary>Tracks each Generate Blueprint request sent to the STBI AgentHost API.</summary>
public sealed class BlueprintRequest
{
    public Guid RequestId { get; set; }
    public Guid ClientId { get; set; }

    public string BusinessRequirement { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;

    /// <summary>Optional JSON string describing existing data schema sent to AgentHost.</summary>
    public string? ExistingSchema { get; set; }

    /// <summary>Workflow status returned by AgentHost: Completed | PartiallyValid | Failed.</summary>
    public string Status { get; set; } = "Pending";

    /// <summary>Secure PDF download URL returned by AgentHost — /api/blueprints/{requestId}/pdf.</summary>
    public string? PdfDownloadUrl { get; set; }

    /// <summary>Credits remaining for the tenant after this request (from AgentHost response).</summary>
    public int? CreditsRemaining { get; set; }

    public int? CreditsConsumed { get; set; }

    public string? SubscriptionPlan { get; set; }

    /// <summary>Date when the credit pool resets (from AgentHost response).</summary>
    public DateTimeOffset? ResetDate { get; set; }

    /// <summary>Blob path where the full AgentHost JSON response is stored for AI training.</summary>
    public string? ResponseBlobPath { get; set; }

    /// <summary>Serialized warnings[] array from AgentHost response.</summary>
    public string? WarningsJson { get; set; }

    public string? RequestedByEmail { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
