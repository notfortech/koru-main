using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.API.Authorization;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.API.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = AuthorizationPolicies.PortalAdminPolicy)]
public class AdminMaintenanceController : ControllerBase
{
    private readonly IAdminMaintenanceService _maintenanceService;

    public AdminMaintenanceController(IAdminMaintenanceService maintenanceService)
    {
        _maintenanceService = maintenanceService;
    }

    [HttpPost("revalidate-upload")]
    public async Task<ActionResult> RevalidateUpload([FromQuery] string? clientId, [FromQuery] string? filePath, CancellationToken cancellationToken)
    {
        await _maintenanceService.RevalidateUploadAsync(clientId, filePath, cancellationToken);
        return Accepted();
    }

    [HttpPost("reprocess-job")]
    public async Task<ActionResult> ReprocessJob([FromQuery] Guid jobId, CancellationToken cancellationToken)
    {
        await _maintenanceService.ReprocessJobAsync(jobId, cancellationToken);
        return Accepted();
    }

    [HttpPost("refresh-dataset")]
    public async Task<ActionResult> RefreshDataset([FromQuery] string? clientId, CancellationToken cancellationToken)
    {
        await _maintenanceService.RefreshDatasetAsync(clientId, cancellationToken);
        return Accepted();
    }
}
