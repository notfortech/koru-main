using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.DTOs.Common;

namespace StudioTechBI.Application.Interfaces;

public interface ITenantService
{
    Task<TenantDto> CreateAsync(TenantCreateDto dto, CancellationToken cancellationToken = default);
    Task<PaginatedResult<TenantDto>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<TenantDto?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<TenantDto?> UpdateAsync(Guid tenantId, TenantUpdateDto dto, CancellationToken cancellationToken = default);
    Task<TenantDto?> SetStatusAsync(Guid tenantId, TenantStatusDto dto, CancellationToken cancellationToken = default);
}
