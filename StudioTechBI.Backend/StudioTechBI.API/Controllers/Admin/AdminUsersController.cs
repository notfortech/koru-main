using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.API.Authorization;
using StudioTechBI.API.Controllers;
using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.API.Controllers.Admin;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = AuthorizationPolicies.PortalAdminPolicy)]
public class AdminUsersController : BaseApiController
{
    private readonly IAdminUserService _adminUserService;

    public AdminUsersController(IAdminUserService adminUserService)
    {
        _adminUserService = adminUserService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AdminUserCreateDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _adminUserService.CreateAsync(dto, cancellationToken);
            return HandleResult(ApiResponse<AdminUserDto>.SuccessResponse(result, "User created."));
        }
        catch (InvalidOperationException ex)
        {
            return HandleResult(ApiResponse<AdminUserDto>.ErrorResponse(ex.Message));
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await _adminUserService.GetPagedAsync(page, pageSize, cancellationToken);
        return HandleResult(ApiResponse<PaginatedResult<AdminUserDto>>.SuccessResponse(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var user = await _adminUserService.GetByIdAsync(id, cancellationToken);
        if (user == null)
            return NotFound();
        return HandleResult(ApiResponse<AdminUserDto>.SuccessResponse(user));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AdminUserUpdateDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _adminUserService.UpdateAsync(id, dto, cancellationToken);
            if (result == null)
                return NotFound();
            return HandleResult(ApiResponse<AdminUserDto>.SuccessResponse(result, "User updated."));
        }
        catch (InvalidOperationException ex)
        {
            return HandleResult(ApiResponse<AdminUserDto>.ErrorResponse(ex.Message));
        }
    }

    [HttpPatch("{id:guid}/disable")]
    public async Task<IActionResult> Disable(Guid id, [FromBody] AdminUserDisableDto dto, CancellationToken cancellationToken)
    {
        var result = await _adminUserService.DisableAsync(id, dto, cancellationToken);
        if (result == null)
            return NotFound();
        return HandleResult(ApiResponse<AdminUserDto>.SuccessResponse(result, "User status updated."));
    }

    [HttpPost("{id:guid}/roles")]
    public async Task<IActionResult> AssignRoles(Guid id, [FromBody] AssignRolesDto dto, CancellationToken cancellationToken)
    {
        var result = await _adminUserService.AssignRolesAsync(id, dto, cancellationToken);
        if (result == null)
            return NotFound();
        return HandleResult(ApiResponse<AdminUserDto>.SuccessResponse(result, "Roles assigned."));
    }
}
