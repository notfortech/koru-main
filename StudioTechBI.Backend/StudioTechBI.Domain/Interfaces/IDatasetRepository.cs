using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Domain.Interfaces;

public interface IDatasetRepository : IRepository<InsightDataset>
{
    Task<InsightDataset?> GetLatestByModelIdAsync(Guid modelId, CancellationToken cancellationToken = default);

    /// <summary>Completed dataset linked to Power BI (idempotent select).</summary>
    Task<InsightDataset?> GetActiveByModelIdAsync(Guid modelId, CancellationToken cancellationToken = default);

    /// <summary>Active datasets keyed by model id (latest per model).</summary>
    Task<IReadOnlyDictionary<Guid, InsightDataset>> GetActiveDatasetsByModelIdsAsync(
        IEnumerable<Guid> modelIds,
        CancellationToken cancellationToken = default);
}
