namespace StudioTechBI.Domain.Entities;

/// <summary>Tracks orchestration / long-running work for an insight model.</summary>
public class InsightJob : BaseEntity
{
    public Guid ModelId { get; set; }
    public InsightModel Model { get; set; } = null!;

    /// <summary>Queued, Processing, Completed, Failed</summary>
    public string Status { get; set; } = InsightJobStatuses.Queued;

    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public string? ErrorMessage { get; set; }
}

public static class InsightJobStatuses
{
    public const string Queued = "Queued";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}
