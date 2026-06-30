using StudioTechBI.Application.DTOs.Blueprints;
using StudioTechBI.Application.DTOs.Common;

namespace StudioTechBI.Application.Interfaces;

/// <summary>
/// High-level AI Gateway consumed by the API controller.
/// Orchestrates request validation, job creation, queuing, and result retrieval.
/// Designed to be extended for future AgentHost capabilities (Story Generation,
/// Report Review, Dashboard Regeneration, Knowledge Packs).
/// </summary>
public interface IAiGateway
{
    Task<BlueprintGenerationJobDto> QueueBlueprintGenerationAsync(
        GenerateBlueprintRequest request,
        string createdBy,
        CancellationToken cancellationToken = default);

    Task<BlueprintGenerationJobDto?> GetGenerationStatusAsync(
        Guid generationId,
        CancellationToken cancellationToken = default);

    Task<(IEnumerable<BlueprintDto> Items, int TotalCount)> GetBlueprintsAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<BlueprintDto?> GetBlueprintAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Stream?> GetBlueprintPdfAsync(Guid id, CancellationToken cancellationToken = default);

    Task<string?> GetBlueprintJsonAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeleteBlueprintAsync(Guid id, CancellationToken cancellationToken = default);
}
