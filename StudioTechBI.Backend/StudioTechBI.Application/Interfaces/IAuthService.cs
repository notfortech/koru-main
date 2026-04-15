using StudioTechBI.Application.DTOs.Auth;

namespace StudioTechBI.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task<LoginResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);
    Task<LoginResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default);
    Task<bool> RevokeTokenAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Creates a time-limited reset token (email integration optional; see PasswordReset:ExposeTokenInResponse for dev).</summary>
    Task<ForgotPasswordResponseDto> RequestPasswordResetAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Sets a new password when token and email match a valid pending reset.</summary>
    Task ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken = default);
}
