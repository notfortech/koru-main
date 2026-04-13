using Microsoft.EntityFrameworkCore;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Domain.Interfaces;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.Infrastructure.Repositories;

public class DatasetRepository : Repository<InsightDataset>, IDatasetRepository
{
    public DatasetRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<InsightDataset?> GetLatestByModelIdAsync(Guid modelId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(e => !e.IsDeleted && e.ModelId == modelId)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
