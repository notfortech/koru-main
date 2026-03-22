using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.API.Authorization;
using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.API.Controllers.Admin;

[ApiController]
[Route("api/admin/templates")]
[Authorize(Policy = AuthorizationPolicies.PortalAdminPolicy)]
public class AdminTemplatesController : ControllerBase
{
    private readonly ITemplateService _templateService;

    public AdminTemplatesController(ITemplateService templateService)
    {
        _templateService = templateService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TemplateDto>>> GetAll(CancellationToken cancellationToken)
    {
        var list = await _templateService.GetAllAsync(cancellationToken);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TemplateDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var template = await _templateService.GetByIdAsync(id, cancellationToken);
        if (template == null)
            return NotFound();
        return Ok(template);
    }

    [HttpPost]
    public async Task<ActionResult<TemplateDto>> Create([FromForm] TemplateCreateDto dto, IFormFile? file, CancellationToken cancellationToken)
    {
        Stream? stream = null;
        string? fileName = null;
        if (file != null)
        {
            stream = file.OpenReadStream();
            fileName = file.FileName;
        }
        try
        {
            var template = await _templateService.CreateAsync(dto, stream, fileName, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = template.TemplateId }, template);
        }
        finally
        {
            stream?.Dispose();
        }
    }
}
