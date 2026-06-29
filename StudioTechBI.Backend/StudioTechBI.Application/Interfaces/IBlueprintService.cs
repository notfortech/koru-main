using StudioTechBI.Application.DTOs.Blueprint;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Application.Interfaces;

public interface IBlueprintService
{
    Task<BlueprintGenerateResponseDto> GenerateAsync(
        string clientCode,
        string businessRequirement,
        string? industry,
        string? existingSchema,
        string requestedByEmail,
        CancellationToken ct = default);

    /// <summary>Returns the most recent credit snapshot for the client from their last blueprint request.</summary>
    Task<BlueprintCreditsDto?> GetCreditsAsync(string clientCode, CancellationToken ct = default);

    Task<IReadOnlyList<BlueprintRequest>> GetRequestsAsync(string clientCode, CancellationToken ct = default);

    /// <summary>
    /// Streams the PDF from AgentHost for a given requestId.
    /// Returns null if not found or PDF URL was not recorded.
    /// </summary>
    Task<Stream?> GetPdfStreamAsync(string clientCode, Guid requestId, CancellationToken ct = default);
}
