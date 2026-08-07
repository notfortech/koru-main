namespace StudioTechBI.Application.Interfaces;

/// <summary>
/// In-process queue that decouples the HTTP controller (202 Accepted) from the background worker
/// that calls the schema-model directory match. Mirrors IReportModelGenerationQueue exactly.
/// </summary>
public interface ISchemaModelMatchQueue
{
    ValueTask EnqueueAsync(Guid matchId, CancellationToken cancellationToken = default);

    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken = default);
}
