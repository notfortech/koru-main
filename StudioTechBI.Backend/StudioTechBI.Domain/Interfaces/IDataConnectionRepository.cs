using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Domain.Interfaces;

public interface IDataConnectionRepository : IRepository<DataConnection>
{
    Task<IReadOnlyList<DataConnection>> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken = default);
}
