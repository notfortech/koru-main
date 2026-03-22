using Microsoft.AspNetCore.Mvc;
using StudioTechBI.Application.DTOs.Auth;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : BaseApiController
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Login with email and password
    /// </summary>
    /// <param name="request">Login request with email and password</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JWT access token, refresh token, and user information</returns>
    /// <response code="200">Login successful, returns access token and refresh token</response>
    /// <response code="400">Invalid request or invalid credentials</response>
    /// <response code="500">Server error</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid request", errors));
            }

            var result = await _authService.LoginAsync(request, cancellationToken);

            return Ok(ApiResponse<LoginResponseDto>.SuccessResponse(result, "Login successful"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse<object>.ErrorResponse("Authentication failed", new List<string> { ex.Message }));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResponse(
                "An error occurred during login",
                new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Register a new user account
    /// </summary>
    /// <param name="request">Registration request with user details and password</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JWT access token and user information</returns>
    /// <response code="200">Registration successful</response>
    /// <response code="400">Invalid request or user already exists</response>
    /// <response code="500">Server error</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequestDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid request", errors));
            }

            var result = await _authService.RegisterAsync(request, cancellationToken);

            return Ok(ApiResponse<LoginResponseDto>.SuccessResponse(result, "Registration successful"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("Registration failed", new List<string> { ex.Message }));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResponse(
                "An error occurred during registration",
                new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Refresh access token using refresh token
    /// </summary>
    /// <param name="request">Refresh token request with expired access token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New JWT access token and refresh token</returns>
    /// <response code="200">Token refreshed successfully</response>
    /// <response code="400">Invalid request or invalid refresh token</response>
    /// <response code="401">Unauthorized - invalid token</response>
    /// <response code="500">Server error</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RefreshToken(
        [FromBody] RefreshTokenRequestDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid request", errors));
            }

            var result = await _authService.RefreshTokenAsync(request, cancellationToken);

            return Ok(ApiResponse<LoginResponseDto>.SuccessResponse(result, "Token refreshed successfully"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse<object>.ErrorResponse("Token refresh failed", new List<string> { ex.Message }));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResponse(
                "An error occurred during token refresh",
                new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Logout by revoking refresh token
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success message</returns>
    /// <response code="200">Logout successful</response>
    /// <response code="401">Unauthorized - user not authenticated</response>
    /// <response code="500">Server error</response>
    [HttpPost("logout")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(ApiResponse<object>.ErrorResponse("User not authenticated", new List<string> { "Unable to identify user" }));
            }

            await _authService.RevokeTokenAsync(userId, cancellationToken);

            return Ok(ApiResponse<object>.SuccessResponse(null!, "Logout successful"));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResponse(
                "An error occurred during logout",
                new List<string> { ex.Message }));
        }
    }
}
