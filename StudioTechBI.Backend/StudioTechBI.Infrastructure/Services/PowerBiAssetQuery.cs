using Microsoft.EntityFrameworkCore;
using StudioTechBI.Application.DTOs.PowerBI;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.Infrastructure.Services;

public class PowerBiAssetQuery : IPowerBiAssetQuery
{
    private readonly ApplicationDbContext _db;

    public PowerBiAssetQuery(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PowerBiAssetDto?> GetActiveAssetByClientCodeAsync(
        string clientCode,
        string reportType,
        CancellationToken cancellationToken = default)
    {
        var code = (clientCode ?? "").Trim();
        if (string.IsNullOrEmpty(code))
            return null;

        var codeLower = code.ToLowerInvariant();

        reportType = (reportType ?? "").Trim();

        // Resolve by TemplateName first (template name == clientCode, e.g. AU-004).
        // This allows sharing the same TemplateId across many excel sheets inside the client blob folder.
        var template = await _db.Templates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => !t.IsDeleted && t.TemplateName.ToLower() == codeLower, cancellationToken);

        // Also resolve the client so we can fall back to client-scoped assets.
        var client = await _db.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => !c.IsDeleted && c.ClientCode != null && c.ClientCode.ToLower() == codeLower, cancellationToken);

        var templateId = template?.Id;
        var clientId = client?.Id;

        if (!templateId.HasValue && !clientId.HasValue)
            return null;

        var assetQuery = _db.PowerBiAssets
            .AsNoTracking()
            .Where(a => a.IsActive);

        // Candidate assets: either template-scoped or client-scoped.
        assetQuery = assetQuery.Where(a =>
            (templateId.HasValue && a.TemplateId == templateId.Value) ||
            (clientId.HasValue && a.ClientId == clientId.Value));

        // Priority:
        // 1) TemplateId match + ClientId match
        // 2) TemplateId match only
        // 3) ClientId match only
        // Additionally prefer ReportType match (e.g. monthly).
        var asset = await assetQuery
            .OrderBy(a =>
                (templateId.HasValue && a.TemplateId == templateId.Value)
                    ? ((clientId.HasValue && a.ClientId == clientId.Value) ? 0 : 1)
                    : 2)
            .ThenBy(a => a.ReportType == reportType ? 0 : 1)
            .ThenByDescending(a => a.UpdatedAt ?? a.CreatedAt ?? DateTime.MinValue)
            .FirstOrDefaultAsync(cancellationToken);

        if (asset == null)
            return null;

        return new PowerBiAssetDto
        {
            ClientId = asset.ClientId,
            ReportType = asset.ReportType,
            WorkspaceId = asset.WorkspaceId,
            DatasetId = asset.DatasetId,
            ReportId = asset.ReportId,
            CapacityId = asset.CapacityId
        };
    }
}

