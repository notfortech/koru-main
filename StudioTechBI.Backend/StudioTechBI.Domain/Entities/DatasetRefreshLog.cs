namespace StudioTechBI.Domain.Entities;

/// <summary>Log entry for a dataset refresh operation (reporting.DatasetRefreshLogs).</summary>
public class DatasetRefreshLog
{
    public Guid RefreshId { get; set; }
    public Guid? ClientId { get; set; }
    public string? DatasetName { get; set; }
    public string? Status { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? ErrorMessage { get; set; }
}
