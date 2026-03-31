using StudioTechBI.Application.DTOs.Dashboard;

namespace StudioTechBI.Application.Interfaces;

public interface IClientPortalDashboardService
{
    /// <param name="months">Chart and KPI window length (default 6, max 12).</param>
    Task<ClientDashboardResponseDto> GetDashboardAsync(
        DashboardRequestContext context,
        int months,
        CancellationToken cancellationToken = default);
}
