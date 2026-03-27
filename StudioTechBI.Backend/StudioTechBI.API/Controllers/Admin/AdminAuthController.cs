using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<AdminAuthController> _logger;

    public AdminAuthController(IAdminAuthService authService, ILogger<AdminAuthController> logger)
    {
        _authService = authService;
        _logger = logger;
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
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception during admin login.");
            return StatusCode(500, new { message = "An error occurred during admin login." });
        }
    }

    [HttpGet("me")]
    [Authorize(Roles = "Admin,SuperAdmin,OperationsAdmin,SupportAdmin")]
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
