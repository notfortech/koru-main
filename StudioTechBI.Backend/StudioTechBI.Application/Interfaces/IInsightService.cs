using StudioTechBI.Application.DTOs.Insight;

namespace StudioTechBI.Application.Interfaces;

public interface IInsightService
{
    Task<IReadOnlyList<ModelDto>> GenerateModelsAsync(Guid clientId, string? blobPathOverride, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModelRecommendationDto>> GenerateModelSuggestionsFromBlobAsync(
        Guid clientId,
        string blobPath,
        CancellationToken cancellationToken = default);

    /// <param name="queueAsync">When true, queues orchestration and returns immediately with <see cref="SelectModelResponseDto.Queued"/> true.</param>
    Task<SelectModelResponseDto> SelectModelAsync(Guid modelId, bool queueAsync = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModelDto>> GetModelsForClientAsync(Guid clientId, CancellationToken cancellationToken = default);

    /// <summary>Stores a single AI draft model (idempotent by external model id) and returns a UI-safe summary.</summary>
    Task<AiModelDraftSummaryDto> StoreAiDraftModelAsync(Guid clientId, AiModelResponse ai, CancellationToken cancellationToken = default);

    /// <summary>Approves a draft model once, stores consent, and triggers report generation.</summary>
    Task<SelectModelResponseDto> ApproveAiModelAsync(Guid modelId, Guid clientId, CancellationToken cancellationToken = default);
}
