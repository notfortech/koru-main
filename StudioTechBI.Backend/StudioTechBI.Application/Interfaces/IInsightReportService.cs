using StudioTechBI.Application.DTOs.Insight;

namespace StudioTechBI.Application.Interfaces;

public interface IInsightReportService
{
    /// <summary>Embed token for the active InsightDataset tied to <paramref name="modelId"/>.</summary>
    Task<InsightReportEmbedDto?> GetInsightReportEmbedAsync(Guid modelId, CancellationToken cancellationToken = default);
}
