using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.API.Authorization;
using StudioTechBI.API.Controllers;
using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.API.Controllers.Admin;

[ApiController]
[Route("api/admin/tenants")]
[Authorize(Roles = "Admin,SuperAdmin,OperationsAdmin,SupportAdmin")]
public class AdminTenantsController : BaseApiController
{
    private readonly ITenantService _tenantService;

    public AdminTenantsController(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TenantCreateDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _tenantService.CreateAsync(dto, cancellationToken);
            return HandleResult(ApiResponse<TenantDto>.SuccessResponse(result, "Tenant created."));
        }
        catch (InvalidOperationException)
        {
            return HandleResult(ApiResponse<TenantDto>.ErrorResponse("Tenant request could not be completed."));
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await _tenantService.GetPagedAsync(page, pageSize, cancellationToken);
        return HandleResult(ApiResponse<PaginatedResult<TenantDto>>.SuccessResponse(result));
    }

    [HttpGet("{tenantId:guid}")]
    public async Task<IActionResult> GetById(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await _tenantService.GetByIdAsync(tenantId, cancellationToken);
        if (tenant == null)
            return NotFound();
        return HandleResult(ApiResponse<TenantDto>.SuccessResponse(tenant));
    }

    [HttpPut("{tenantId:guid}")]
    public async Task<IActionResult> Update(Guid tenantId, [FromBody] TenantUpdateDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _tenantService.UpdateAsync(tenantId, dto, cancellationToken);
            if (result == null)
                return NotFound();
            return HandleResult(ApiResponse<TenantDto>.SuccessResponse(result, "Tenant updated."));
        }
        catch (InvalidOperationException)
        {
            return HandleResult(ApiResponse<TenantDto>.ErrorResponse("Tenant update request could not be completed."));
        }
    }

    [HttpPatch("{tenantId:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid tenantId, [FromBody] TenantStatusDto dto, CancellationToken cancellationToken)
    {
        var result = await _tenantService.SetStatusAsync(tenantId, dto, cancellationToken);
        if (result == null)
            return NotFound();
        return HandleResult(ApiResponse<TenantDto>.SuccessResponse(result, "Status updated."));
    }
}
