using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Domain.Interfaces;

namespace StudioTechBI.Application.Services;

public class AdminAuthService : IAdminAuthService
{
    private readonly IRepository<AdminUser> _adminRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminAuthService> _logger;

    public AdminAuthService(
        IRepository<AdminUser> adminRepository,
        IJwtTokenService jwtTokenService,
        IConfiguration configuration,
        ILogger<AdminAuthService> logger)
    {
        _adminRepository = adminRepository;
        _jwtTokenService = jwtTokenService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AdminLoginResponseDto> LoginAsync(AdminLoginRequestDto request, CancellationToken cancellationToken = default)
    {
        // Admin login uses AdminUsers table only (not Users/UserRoles). Email comparison is case-insensitive.
        var email = (request.Email ?? "").Trim().ToLowerInvariant();
        var admin = await _adminRepository.GetByPredicateAsync(
            a => a.Email.ToLower() == email && a.IsActive && !a.IsDeleted,
            cancellationToken);

        if (admin == null)
        {
            _logger.LogWarning("Admin login failed: no admin found for email {Email} (check AdminUsers table)", email);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        var passwordHash = (admin.PasswordHash ?? "").Trim();
        if (string.IsNullOrEmpty(passwordHash) || !BCrypt.Net.BCrypt.Verify(request.Password ?? "", passwordHash))
        {
            _logger.LogWarning("Admin login failed: password mismatch for email {Email}", email);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, admin.Id.ToString()),
            new("AdminId", admin.Id.ToString()),
            new(ClaimTypes.Email, admin.Email),
            new(ClaimTypes.Name, admin.Name),
            new(ClaimTypes.Role, admin.Role.ToString())
        };

        var accessToken = _jwtTokenService.GenerateAccessToken(claims);
        var expMinutes = _configuration.GetValue("JwtSettings:AccessTokenExpirationMinutes", 60);

        _logger.LogInformation("Admin {AdminId} logged in", admin.Id);
        return new AdminLoginResponseDto
        {
            AccessToken = accessToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expMinutes),
            Admin = MapToMeDto(admin)
        };
    }

    public async Task<AdminMeDto?> GetMeAsync(Guid adminId, CancellationToken cancellationToken = default)
    {
        var admin = await _adminRepository.GetByIdAsync(adminId, cancellationToken);
        if (admin == null || !admin.IsActive || admin.IsDeleted)
            return null;
        return MapToMeDto(admin);
    }

    private static AdminMeDto MapToMeDto(AdminUser admin)
    {
        return new AdminMeDto
        {
            AdminId = admin.Id,
            Name = admin.Name,
            Email = admin.Email,
            Role = admin.Role.ToString()
        };
    }
}
