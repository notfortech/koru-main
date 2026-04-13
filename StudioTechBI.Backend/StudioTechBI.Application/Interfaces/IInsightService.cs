using StudioTechBI.Application.DTOs.Insight;

namespace StudioTechBI.Application.Interfaces;

public interface IInsightService
{
    Task<IReadOnlyList<ModelDto>> GenerateModelsAsync(Guid clientId, string? blobPathOverride, CancellationToken cancellationToken = default);

    Task<OrchestratorResultDto> SelectModelAsync(Guid modelId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModelDto>> GetModelsForClientAsync(Guid clientId, CancellationToken cancellationToken = default);
}
