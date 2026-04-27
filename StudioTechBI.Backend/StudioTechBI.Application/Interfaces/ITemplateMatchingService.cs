using StudioTechBI.Application.DTOs.Templates;

namespace StudioTechBI.Application.Interfaces;

public interface ITemplateMatchingService
{
    /// <summary>
    /// Best catalog match score (0..1) for the given client column names against
    /// <c>Templates.RequiredColumnsJson</c> / <c>OptionalColumnsJson</c>.
    /// </summary>
    Task<double> GetBestCatalogMatchScoreAsync(IReadOnlyList<string> clientColumns, CancellationToken cancellationToken = default);

    Task<TemplateMatchResponse> MatchFromBlobAsync(
        string? clientCodeOrId,
        bool useSelectedClient,
        string? blobPath,
        int maxRows,
        bool useAiRefinement,
        CancellationToken cancellationToken = default);

    /// <summary>In-memory path: same ranking as blob match, without reading storage.</summary>
    Task<TemplateMatchResponse> MatchFromColumnsAsync(
        string? clientCodeOrId,
        bool useSelectedClient,
        IReadOnlyList<string> columns,
        bool useAiRefinement,
        CancellationToken cancellationToken = default);
}

