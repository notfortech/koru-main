using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Domain.Interfaces;

public interface IDatasetRepository : IRepository<InsightDataset>
{
    Task<InsightDataset?> GetLatestByModelIdAsync(Guid modelId, CancellationToken cancellationToken = default);
}
