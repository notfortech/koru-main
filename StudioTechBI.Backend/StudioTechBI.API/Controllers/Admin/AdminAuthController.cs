using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.API.Authorization;
using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.Interfaces;
using System.Security.Claims;

namespace StudioTechBI.API.Controllers.Admin;

[ApiController]
[Route("api/admin")]
public class AdminAuthController : ControllerBase
{
    private readonly IAdminAuthService _authService;

    public AdminAuthController(IAdminAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AdminLoginResponseDto>> Login([FromBody] AdminLoginRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _authService.LoginAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpGet("me")]
    [Authorize(Policy = AuthorizationPolicies.PortalAdminPolicy)]
    public async Task<ActionResult<AdminMeDto>> Me(CancellationToken cancellationToken)
    {
        var adminIdClaim = User.FindFirstValue("AdminId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(adminIdClaim) || !Guid.TryParse(adminIdClaim, out var adminId))
            return Unauthorized();
        var admin = await _authService.GetMeAsync(adminId, cancellationToken);
        if (admin == null)
            return Unauthorized();
        return Ok(admin);
    }
}
