using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Application.Interfaces;

public interface IBlueprintRepository
{
    // ── Blueprint ─────────────────────────────────────────────────────────────

    Task<Blueprint?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Blueprint?> GetByProjectAsync(
        Guid tenantId,
        Guid clientId,
        string projectId,
        CancellationToken cancellationToken = default);

    Task<(IEnumerable<Blueprint> Items, int TotalCount)> GetPagedByTenantAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Blueprint> AddAsync(Blueprint blueprint, CancellationToken cancellationToken = default);
    Task UpdateAsync(Blueprint blueprint, CancellationToken cancellationToken = default);
    Task DeleteAsync(Blueprint blueprint, CancellationToken cancellationToken = default);

    // ── BlueprintVersion ──────────────────────────────────────────────────────

    Task<BlueprintVersion?> GetActiveVersionAsync(Guid blueprintId, CancellationToken cancellationToken = default);
    Task<BlueprintVersion> AddVersionAsync(BlueprintVersion version, CancellationToken cancellationToken = default);
    Task UpdateVersionAsync(BlueprintVersion version, CancellationToken cancellationToken = default);

    // ── BlueprintGeneration ───────────────────────────────────────────────────

    Task<BlueprintGeneration?> GetGenerationByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<BlueprintGeneration>> GetPendingGenerationsAsync(CancellationToken cancellationToken = default);
    Task<BlueprintGeneration> AddGenerationAsync(BlueprintGeneration generation, CancellationToken cancellationToken = default);
    Task UpdateGenerationAsync(BlueprintGeneration generation, CancellationToken cancellationToken = default);

    // ── Unit of Work ──────────────────────────────────────────────────────────

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
