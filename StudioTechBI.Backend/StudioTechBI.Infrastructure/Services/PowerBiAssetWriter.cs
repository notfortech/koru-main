using Microsoft.EntityFrameworkCore;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.Infrastructure.Services;

public class PowerBiAssetWriter : IPowerBiAssetWriter
{
    private readonly ApplicationDbContext _db;

    public PowerBiAssetWriter(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task WriteAsync(
        Guid? clientId,
        Guid? templateId,
        string workspaceId,
        string datasetId,
        string? reportId,
        string reportType,
        CancellationToken cancellationToken = default)
    {
        // Client has no TenantId FK yet, so there's no legitimate way to resolve a tenant from
        // clientId today. Falls back to the first Tenant row (or Guid.Empty if none exist) as a
        // stopgap until Client is actually linked to Tenant.
        var tenantId = await _db.Tenants.AsNoTracking().Select(t => t.Id).FirstOrDefaultAsync(cancellationToken);

        var asset = new PowerBiAsset
        {
            AssetId = Guid.NewGuid(),
            TenantId = tenantId,
            ClientId = clientId,
            TemplateId = templateId,
            WorkspaceId = workspaceId,
            DatasetId = datasetId,
            ReportId = reportId,
            ReportType = reportType,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.PowerBiAssets.Add(asset);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
