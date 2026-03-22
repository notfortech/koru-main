namespace StudioTechBI.Application.Interfaces;

/// <summary>Writes dataset refresh log entries to reporting.DatasetRefreshLogs.</summary>
public interface IDatasetRefreshLogWriter
{
    Task LogAsync(Guid? clientId, string? datasetName, string status, DateTime? startTime, DateTime? endTime, string? errorMessage, CancellationToken cancellationToken = default);
}
