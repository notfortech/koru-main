using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Domain.Interfaces;

public interface IModelRepository : IRepository<InsightModel>
{
    Task<IReadOnlyList<InsightModel>> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken = default);
}
