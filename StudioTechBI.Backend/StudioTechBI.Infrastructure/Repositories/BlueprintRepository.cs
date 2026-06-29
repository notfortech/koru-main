using Microsoft.EntityFrameworkCore;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.Infrastructure.Repositories;

public class BlueprintRepository : IBlueprintRepository
{
    private readonly ApplicationDbContext _context;

    public BlueprintRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    // ── Blueprint ─────────────────────────────────────────────────────────────

    public Task<Blueprint?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Blueprints
            .Include(b => b.Versions.Where(v => v.IsActive && !v.IsDeleted))
            .Where(b => !b.IsDeleted && b.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Blueprint?> GetByProjectAsync(
        Guid tenantId,
        Guid clientId,
        string projectId,
        CancellationToken cancellationToken = default) =>
        _context.Blueprints
            .Where(b => !b.IsDeleted
                     && b.TenantId == tenantId
                     && b.ClientId == clientId
                     && b.ProjectId == projectId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<(IEnumerable<Blueprint> Items, int TotalCount)> GetPagedByTenantAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Blueprints
            .Include(b => b.Versions.Where(v => v.IsActive && !v.IsDeleted))
            .Where(b => !b.IsDeleted && b.TenantId == tenantId)
            .OrderByDescending(b => b.CreatedAt);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<Blueprint> AddAsync(Blueprint blueprint, CancellationToken cancellationToken = default)
    {
        blueprint.CreatedAt = DateTime.UtcNow;
        await _context.Blueprints.AddAsync(blueprint, cancellationToken);
        return blueprint;
    }

    public Task UpdateAsync(Blueprint blueprint, CancellationToken cancellationToken = default)
    {
        blueprint.UpdatedAt = DateTime.UtcNow;
        _context.Blueprints.Update(blueprint);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Blueprint blueprint, CancellationToken cancellationToken = default)
    {
        blueprint.IsDeleted = true;
        blueprint.UpdatedAt = DateTime.UtcNow;
        _context.Blueprints.Update(blueprint);
        return Task.CompletedTask;
    }

    // ── BlueprintVersion ──────────────────────────────────────────────────────

    public Task<BlueprintVersion?> GetActiveVersionAsync(Guid blueprintId, CancellationToken cancellationToken = default) =>
        _context.BlueprintVersions
            .Where(v => !v.IsDeleted && v.BlueprintId == blueprintId && v.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<BlueprintVersion> AddVersionAsync(BlueprintVersion version, CancellationToken cancellationToken = default)
    {
        version.CreatedAt = DateTime.UtcNow;
        await _context.BlueprintVersions.AddAsync(version, cancellationToken);
        return version;
    }

    public Task UpdateVersionAsync(BlueprintVersion version, CancellationToken cancellationToken = default)
    {
        version.UpdatedAt = DateTime.UtcNow;
        _context.BlueprintVersions.Update(version);
        return Task.CompletedTask;
    }

    // ── BlueprintGeneration ───────────────────────────────────────────────────

    public Task<BlueprintGeneration?> GetGenerationByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.BlueprintGenerations
            .Where(g => !g.IsDeleted && g.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<IEnumerable<BlueprintGeneration>> GetPendingGenerationsAsync(CancellationToken cancellationToken = default) =>
        _context.BlueprintGenerations
            .Where(g => !g.IsDeleted && g.Status == BlueprintStatuses.Pending)
            .OrderBy(g => g.CreatedAt)
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IEnumerable<BlueprintGeneration>)t.Result, cancellationToken);

    public async Task<BlueprintGeneration> AddGenerationAsync(BlueprintGeneration generation, CancellationToken cancellationToken = default)
    {
        generation.CreatedAt = DateTime.UtcNow;
        await _context.BlueprintGenerations.AddAsync(generation, cancellationToken);
        return generation;
    }

    public Task UpdateGenerationAsync(BlueprintGeneration generation, CancellationToken cancellationToken = default)
    {
        generation.UpdatedAt = DateTime.UtcNow;
        _context.BlueprintGenerations.Update(generation);
        return Task.CompletedTask;
    }

    // ── Unit of Work ──────────────────────────────────────────────────────────

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
