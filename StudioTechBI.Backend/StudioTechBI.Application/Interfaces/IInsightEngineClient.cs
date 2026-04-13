using StudioTechBI.Application.DTOs.Insight;

namespace StudioTechBI.Application.Interfaces;

public interface IInsightEngineClient
{
    Task<IReadOnlyList<ModelDto>> GenerateModelsAsync(GenerateModelRequest request, CancellationToken cancellationToken = default);

    Task<OrchestratorResultDto> SelectModelAsync(Guid modelId, string? validatedDataBlobPath, CancellationToken cancellationToken = default);
}
