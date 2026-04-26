using StudioTechBI.Application.DTOs.Templates;

namespace StudioTechBI.Application.Interfaces;

public interface ITemplateMatchingService
{
    Task<TemplateMatchResponse> MatchFromBlobAsync(
        string? clientCodeOrId,
        bool useSelectedClient,
        string? blobPath,
        int maxRows,
        bool useAiRefinement,
        CancellationToken cancellationToken = default);
}

