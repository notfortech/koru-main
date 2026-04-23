using StudioTechBI.Application.DTOs.Insight;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.Infrastructure.Clients;

/// <summary>Used when InsightEngine:Enabled is false so HttpClient is never called with an invalid base URL.</summary>
public sealed class DisabledInsightEngineClient : IInsightEngineClient
{
    public Task<IReadOnlyList<ModelDto>> GenerateModelsAsync(GenerateModelRequest request, CancellationToken cancellationToken = default) =>
        Task.FromException<IReadOnlyList<ModelDto>>(
            new InvalidOperationException("Insight Engine integration is disabled (InsightEngine:Enabled=false)."));

    public Task<IReadOnlyList<ModelRecommendationDto>> RecommendModelsAsync(SampleRequest request, CancellationToken cancellationToken = default) =>
        Task.FromException<IReadOnlyList<ModelRecommendationDto>>(
            new InvalidOperationException("Insight Engine integration is disabled (InsightEngine:Enabled=false)."));

    public Task<OrchestratorResultDto> SelectModelAsync(Guid modelId, string? validatedDataBlobPath, CancellationToken cancellationToken = default) =>
        Task.FromException<OrchestratorResultDto>(
            new InvalidOperationException("Insight Engine integration is disabled (InsightEngine:Enabled=false)."));
}
