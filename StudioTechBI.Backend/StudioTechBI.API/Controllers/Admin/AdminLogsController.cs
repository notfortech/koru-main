using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.API.Authorization;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.API.Controllers.Admin;

[ApiController]
[Route("api/admin/logs")]
[Authorize(Roles = "Admin,SuperAdmin,OperationsAdmin,SupportAdmin")]
public class AdminLogsController : ControllerBase
{
    private readonly IAdminLoggingService _loggingService;

    public AdminLogsController(IAdminLoggingService loggingService)
    {
        _loggingService = loggingService;
    }

    [HttpGet("functional")]
    public async Task<ActionResult<IReadOnlyList<Application.DTOs.Admin.FunctionalLogDto>>> GetFunctional(
        [FromQuery] int? limit,
        [FromQuery] Guid? clientId,
        CancellationToken cancellationToken)
    {
        var list = await _loggingService.GetFunctionalLogsAsync(limit, clientId, cancellationToken);
        return Ok(list);
    }

    [HttpGet("technical")]
    public async Task<ActionResult<IReadOnlyList<Application.DTOs.Admin.TechnicalLogDto>>> GetTechnical(
        [FromQuery] int? limit,
        [FromQuery] string? level,
        CancellationToken cancellationToken)
    {
        var list = await _loggingService.GetTechnicalLogsAsync(limit, level, cancellationToken);
        return Ok(list);
    }
}
