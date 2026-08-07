using System.Runtime.CompilerServices;
using System.Threading.Channels;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.Infrastructure.AI;

/// <summary>
/// Singleton in-process queue backed by System.Threading.Channels. Registered as a singleton so
/// both the controller (producer) and the background worker (consumer) share the same instance.
/// Mirrors BlueprintGenerationQueue exactly.
/// </summary>
public sealed class ReportModelGenerationQueue : IReportModelGenerationQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true });

    public ValueTask EnqueueAsync(Guid generationId, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(generationId, cancellationToken);

    public async IAsyncEnumerable<Guid> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var id in _channel.Reader.ReadAllAsync(cancellationToken))
            yield return id;
    }
}
