namespace StudioTechBI.Domain.Entities;

/// <summary>Auditable user consent record for AI-generated model approval.</summary>
public sealed class ModelConsent : BaseEntity
{
    public Guid ModelId { get; set; }
    public InsightModel Model { get; set; } = null!;

    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;

    public DateTime ApprovedAt { get; set; }

    /// <summary>Stored summary (no TOM). Typically the same JSON shown to the user.</summary>
    public string SummaryJson { get; set; } = string.Empty;
}

