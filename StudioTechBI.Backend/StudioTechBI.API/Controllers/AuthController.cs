using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.Auth;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : BaseApiController
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
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
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<object>.ErrorResponse("Authentication failed", new List<string> { "Invalid credentials." }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception during login.");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("An error occurred during login"));
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
        catch (InvalidOperationException)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("Registration failed", new List<string> { "Registration request could not be completed." }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception during registration.");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("An error occurred during registration"));
        }
    }

    /// <summary>Request a password reset. Response is generic (no email enumeration). In Development, reset token may be returned when PasswordReset:ExposeTokenInResponse is true.</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ForgotPasswordResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequestDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid request", errors));
            }

            var result = await _authService.RequestPasswordResetAsync(request, cancellationToken);
            return Ok(ApiResponse<ForgotPasswordResponseDto>.SuccessResponse(result, result.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception during forgot-password.");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("An error occurred."));
        }
    }

    /// <summary>Confirm new password using the token from email (or dev response).</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequestDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid request", errors));
            }

            await _authService.ResetPasswordAsync(request, cancellationToken);
            return Ok(ApiResponse<object>.SuccessResponse(null!, "Password has been updated. You can sign in with your new password."));
        }
        catch (InvalidOperationException)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("Reset failed", new List<string> { "Invalid or expired token, or passwords do not match." }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception during reset-password.");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("An error occurred."));
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
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<object>.ErrorResponse("Token refresh failed", new List<string> { "Invalid token or refresh token." }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception during token refresh.");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("An error occurred during token refresh"));
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
            _logger.LogError(ex, "Unhandled exception during logout.");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("An error occurred during logout"));
        }
    }
}
