using StudioTechBI.Application.DTOs.Insight;

namespace StudioTechBI.Application.Interfaces;

public interface IInsightService
{
    Task<IReadOnlyList<ModelDto>> GenerateModelsAsync(Guid clientId, string? blobPathOverride, CancellationToken cancellationToken = default);

    /// <param name="queueAsync">When true, queues orchestration and returns immediately with <see cref="SelectModelResponseDto.Queued"/> true.</param>
    Task<SelectModelResponseDto> SelectModelAsync(Guid modelId, bool queueAsync = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModelDto>> GetModelsForClientAsync(Guid clientId, CancellationToken cancellationToken = default);
}
