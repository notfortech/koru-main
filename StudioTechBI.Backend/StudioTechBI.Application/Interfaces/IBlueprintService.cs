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

    /// <summary>Returns the most recent credit snapshot for the client from their last blueprint.</summary>
    Task<BlueprintCreditsDto?> GetCreditsAsync(string clientCode, CancellationToken ct = default);

    Task<IReadOnlyList<Blueprint>> GetRequestsAsync(string clientCode, CancellationToken ct = default);

    /// <summary>Streams the PDF from AgentHost for the given blueprint Id. Returns null if not found.</summary>
    Task<Stream?> GetPdfStreamAsync(string clientCode, Guid blueprintId, CancellationToken ct = default);
}
