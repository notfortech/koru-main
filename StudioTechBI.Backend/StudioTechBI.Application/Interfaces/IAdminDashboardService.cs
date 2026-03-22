using StudioTechBI.Application.DTOs.Admin;

namespace StudioTechBI.Application.Interfaces;

public interface IAdminDashboardService
{
    Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
}
