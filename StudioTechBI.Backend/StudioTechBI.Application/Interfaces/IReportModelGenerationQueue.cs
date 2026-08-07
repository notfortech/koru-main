namespace StudioTechBI.Application.Interfaces;

/// <summary>
/// In-process queue that decouples the HTTP controller (202 Accepted) from the background worker
/// that calls the Report Designer AI model. Mirrors IBlueprintGenerationQueue exactly.
/// </summary>
public interface IReportModelGenerationQueue
{
    ValueTask EnqueueAsync(Guid generationId, CancellationToken cancellationToken = default);

    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken = default);
}
