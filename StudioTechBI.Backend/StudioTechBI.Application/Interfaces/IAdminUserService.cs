using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.DTOs.Common;

namespace StudioTechBI.Application.Interfaces;

public interface IAdminUserService
{
    Task<AdminUserDto> CreateAsync(AdminUserCreateDto dto, CancellationToken cancellationToken = default);
    Task<PaginatedResult<AdminUserDto>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminUserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AdminUserDto?> UpdateAsync(Guid id, AdminUserUpdateDto dto, CancellationToken cancellationToken = default);
    Task<AdminUserDto?> DisableAsync(Guid id, AdminUserDisableDto dto, CancellationToken cancellationToken = default);
    Task<AdminUserDto?> AssignRolesAsync(Guid id, AssignRolesDto dto, CancellationToken cancellationToken = default);
}
