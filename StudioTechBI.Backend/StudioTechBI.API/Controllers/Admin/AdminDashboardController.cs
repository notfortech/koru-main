using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.API.Authorization;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.API.Controllers.Admin;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = "Admin,SuperAdmin,OperationsAdmin,SupportAdmin")]
public class AdminDashboardController : ControllerBase
{
    private readonly IAdminDashboardService _dashboardService;

    public AdminDashboardController(IAdminDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<ActionResult<Application.DTOs.Admin.DashboardDto>> Get(CancellationToken cancellationToken)
    {
        var dashboard = await _dashboardService.GetDashboardAsync(cancellationToken);
        return Ok(dashboard);
    }
}
