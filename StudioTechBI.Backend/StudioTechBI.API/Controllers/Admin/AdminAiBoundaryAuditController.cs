using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.API.Controllers.Admin;

/// <summary>
/// Read-only view of the AiBoundaryAuditEvents table — the durable, queryable record of every
/// AI-boundary crossing (see AiBoundaryAuditEvent and IAiBoundaryAuditService). This is the
/// thing to point to as proof of what the AI did or didn't see, not the application log.
/// </summary>
[ApiController]
[Route("api/admin/ai-boundary-audit-log")]
[Authorize(Roles = "Admin,SuperAdmin,OperationsAdmin,SupportAdmin")]
public class AdminAiBoundaryAuditController : ControllerBase
{
    private readonly IAiBoundaryAuditService _auditService;

    public AdminAiBoundaryAuditController(IAiBoundaryAuditService auditService)
    {
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AiBoundaryAuditEventDto>>> GetRecent(
        [FromQuery] int? limit,
        [FromQuery] string? correlationId,
        [FromQuery] string? operation,
        [FromQuery] bool? success,
        CancellationToken cancellationToken)
    {
        var list = await _auditService.GetRecentAsync(limit, correlationId, operation, success, cancellationToken);
        return Ok(list);
    }
}
