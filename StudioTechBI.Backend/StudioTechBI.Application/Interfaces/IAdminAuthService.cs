using StudioTechBI.Application.DTOs.Admin;

namespace StudioTechBI.Application.Interfaces;

public interface IAdminAuthService
{
    Task<AdminLoginResponseDto> LoginAsync(AdminLoginRequestDto request, CancellationToken cancellationToken = default);
    Task<AdminMeDto?> GetMeAsync(Guid adminId, CancellationToken cancellationToken = default);
}
