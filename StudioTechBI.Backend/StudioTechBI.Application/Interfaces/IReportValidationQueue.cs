namespace StudioTechBI.Application.Interfaces;

/// <summary>
/// In-process queue that decouples the HTTP controller (202 Accepted) from the background worker
/// that runs a validation run's checks. Backed by System.Threading.Channels — same pattern as
/// IBlueprintGenerationQueue.
/// </summary>
public interface IReportValidationQueue
{
    ValueTask EnqueueAsync(Guid runId, CancellationToken cancellationToken = default);

    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken = default);
}
