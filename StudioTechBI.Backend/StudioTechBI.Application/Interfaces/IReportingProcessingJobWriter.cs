namespace StudioTechBI.Application.Interfaces;

/// <summary>Writes processing job lifecycle to reporting.ProcessingJobs.</summary>
public interface IReportingProcessingJobWriter
{
    Task StartAsync(Guid jobId, Guid? clientId, string? fileName, string? blobPath, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid jobId, string status, string? currentStep = null, string? errorMessage = null, DateTime? completedDate = null, CancellationToken cancellationToken = default);
}
