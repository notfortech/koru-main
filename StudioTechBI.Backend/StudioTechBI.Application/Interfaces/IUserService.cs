using StudioTechBI.Application.DTOs.Auth;
using StudioTechBI.Application.DTOs.Common;

namespace StudioTechBI.Application.Interfaces;

public interface IUserService
{
    Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UserDto?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<PaginatedResult<UserDto>> GetAllAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<UserDto> CreateAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);
    Task<UserDto> UpdateAsync(Guid id, UserDto request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
