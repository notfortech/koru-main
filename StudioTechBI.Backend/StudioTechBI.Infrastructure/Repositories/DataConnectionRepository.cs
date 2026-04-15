using Microsoft.EntityFrameworkCore;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Domain.Interfaces;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.Infrastructure.Repositories;

public class DataConnectionRepository : Repository<DataConnection>, IDataConnectionRepository
{
    public DataConnectionRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<DataConnection>> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(e => !e.IsDeleted && e.ClientId == clientId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
