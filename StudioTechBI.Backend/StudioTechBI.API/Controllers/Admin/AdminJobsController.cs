using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.API.Authorization;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.API.Controllers.Admin;

[ApiController]
[Route("api/admin/jobs")]
[Authorize(Roles = "Admin,SuperAdmin,OperationsAdmin,SupportAdmin")]
public class AdminJobsController : ControllerBase
{
    private readonly IProcessingJobService _jobService;

    public AdminJobsController(IProcessingJobService jobService)
    {
        _jobService = jobService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Application.DTOs.Admin.ProcessingJobDto>>> GetAll([FromQuery] int? limit, CancellationToken cancellationToken)
    {
        var list = await _jobService.GetAllAsync(limit, cancellationToken);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Application.DTOs.Admin.ProcessingJobDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var job = await _jobService.GetByIdAsync(id, cancellationToken);
        if (job == null)
            return NotFound();
        return Ok(job);
    }
}
