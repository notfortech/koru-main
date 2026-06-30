namespace StudioTechBI.Application.Interfaces;

/// <summary>
/// In-process queue that decouples the HTTP controller (202 Accepted) from the
/// background worker that calls AgentHost. Backed by System.Threading.Channels.
/// </summary>
public interface IBlueprintGenerationQueue
{
    ValueTask EnqueueAsync(Guid generationId, CancellationToken cancellationToken = default);

    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken = default);
}
