using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.DTOs.Auth;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Domain.Interfaces;

namespace StudioTechBI.Application.Services;

public class AuthService : BaseService, IAuthService
{
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<Role> _roleRepository;
    private readonly IRepository<UserRole> _userRoleRepository;
    private readonly IRepository<Client> _clientRepository;
    private readonly IRepository<RegistrationAttempt> _registrationAttemptRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AuthService> _logger;
    private readonly IClientByCompanyQuery _clientByCompanyQuery;
    private readonly IBlobSasUriProvider _sasUriProvider;
    private readonly PasswordResetOptions _passwordResetOptions;

    private static readonly TimeSpan LogoSasValidFor = TimeSpan.FromHours(24);

    public AuthService(
        IRepository<User> userRepository,
        IRepository<Role> roleRepository,
        IRepository<UserRole> userRoleRepository,
        IRepository<Client> clientRepository,
        IRepository<RegistrationAttempt> registrationAttemptRepository,
        IJwtTokenService jwtTokenService,
        IUnitOfWork unitOfWork,
        ILogger<AuthService> logger,
        IClientByCompanyQuery clientByCompanyQuery,
        IBlobSasUriProvider sasUriProvider,
        IOptions<PasswordResetOptions> passwordResetOptions)
        : base(unitOfWork)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _clientRepository = clientRepository;
        _registrationAttemptRepository = registrationAttemptRepository;
        _jwtTokenService = jwtTokenService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _clientByCompanyQuery = clientByCompanyQuery;
        _sasUriProvider = sasUriProvider;
        _passwordResetOptions = passwordResetOptions.Value;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByPredicateAsync(u => u.Email == request.Email.ToLower() && !u.IsDeleted, cancellationToken);

        if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
        {
            _logger.LogWarning(
                "User login attempt failed for email {Email}. Reason=InvalidCredentials",
                request.Email);
            throw new UnauthorizedAccessException("Invalid email or password");
        }

        if (!user.IsActive)
        {
            _logger.LogWarning(
                "User login attempt failed for email {Email}. Reason=AccountInactive",
                request.Email);
            throw new UnauthorizedAccessException("User account is inactive");
        }

        var roles = await GetUserRolesAsync(user.Id, cancellationToken);
        var client = await ResolveClientForUserAsync(user, cancellationToken);
        var claims = GenerateClaims(user, roles, client);

        var accessToken = _jwtTokenService.GenerateAccessToken(claims);
        var refreshToken = _jwtTokenService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        user.LastLoginAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "User {UserId} logged in successfully (email {Email}).",
            user.Id,
            user.Email);

        return new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            User = await MapUserToDtoAsync(user, roles, client),
            ClientId = client?.Id ?? user.ClientId,
            ClientCode = client?.ClientCode ?? client?.BlobFolderPath
        };
    }

    public async Task<LoginResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        var attempt = new RegistrationAttempt
        {
            Id = Guid.NewGuid(),
            Email = request.Email.ToLower(),
            Success = false,
            FailureReason = null
        };
        await _registrationAttemptRepository.AddAsync(attempt, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Registration attempt written to DB (pending). Email={Email}", attempt.Email);

        try
        {
            var existingUser = await _userRepository.GetByPredicateAsync(u => u.Email == request.Email.ToLower() && !u.IsDeleted, cancellationToken);

            if (existingUser != null)
            {
                attempt.FailureReason = "User with this email already exists";
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Registration attempt recorded in DB: Failure. Email={Email}, Reason={Reason}", attempt.Email, attempt.FailureReason);
                throw new InvalidOperationException(attempt.FailureReason);
            }

            if (!string.IsNullOrEmpty(request.ConfirmPassword) && request.Password != request.ConfirmPassword)
            {
                attempt.FailureReason = "Passwords do not match";
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Registration attempt recorded in DB: Failure. Email={Email}, Reason={Reason}", attempt.Email, attempt.FailureReason);
                throw new InvalidOperationException(attempt.FailureReason);
            }

            var passwordHash = HashPassword(request.Password);

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = request.Email.Trim().ToLowerInvariant(),
                FirstName = string.IsNullOrWhiteSpace(request.FirstName) ? "User" : request.FirstName.Trim(),
                LastName = (request.LastName ?? "").Trim(),
                PasswordHash = passwordHash,
                IsActive = true
            };

            await _userRepository.AddAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var clientRole = await _roleRepository.GetByPredicateAsync(r => r.Name == "Client" && !r.IsDeleted, cancellationToken);
            if (clientRole != null)
            {
                var userRole = new UserRole
                {
                    UserId = user.Id,
                    RoleId = clientRole.Id
                };
                await _userRoleRepository.AddAsync(userRole, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            var roles = clientRole != null ? new List<string> { clientRole.Name } : new List<string>();
            var client = await ResolveClientForUserAsync(user, cancellationToken);
            var claims = GenerateClaims(user, roles, client);

            var accessToken = _jwtTokenService.GenerateAccessToken(claims);
            var refreshToken = _jwtTokenService.GenerateRefreshToken();

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

            attempt.Success = true;
            attempt.FailureReason = null;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Registration attempt recorded in DB: Success. Email={Email}", attempt.Email);

            return new LoginResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(60),
                User = await MapUserToDtoAsync(user, roles, client),
                ClientId = client?.Id ?? user.ClientId,
                ClientCode = client?.ClientCode ?? client?.BlobFolderPath
            };
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            attempt.FailureReason = ex.Message;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Registration attempt recorded in DB: Failure. Email={Email}, Reason={Reason}", attempt.Email, attempt.FailureReason);
            throw;
        }
    }

    public async Task<LoginResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        var principal = _jwtTokenService.GetPrincipalFromExpiredToken(request.AccessToken);

        if (principal == null)
        {
            throw new UnauthorizedAccessException("Invalid access token");
        }

        var emailClaim = principal.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(emailClaim))
        {
            throw new UnauthorizedAccessException("Invalid token claims");
        }

        var user = await _userRepository.GetByPredicateAsync(u => u.Email == emailClaim && !u.IsDeleted, cancellationToken);

        if (user == null || !user.IsActive || user.RefreshToken != request.RefreshToken || user.RefreshTokenExpiryTime < DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        var roles = await GetUserRolesAsync(user.Id, cancellationToken);
        var client = await ResolveClientForUserAsync(user, cancellationToken);
        var claims = GenerateClaims(user, roles, client);

        var accessToken = _jwtTokenService.GenerateAccessToken(claims);
        var refreshToken = _jwtTokenService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            User = await MapUserToDtoAsync(user, roles, client),
            ClientId = client?.Id ?? user.ClientId,
            ClientCode = client?.ClientCode ?? client?.BlobFolderPath
        };
    }

    public async Task<bool> RevokeTokenAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var userGuid))
        {
            return false;
        }

        var user = await _userRepository.GetByIdAsync(userGuid, cancellationToken);

        if (user == null)
        {
            return false;
        }

        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<ForgotPasswordResponseDto> RequestPasswordResetAsync(
        ForgotPasswordRequestDto request,
        CancellationToken cancellationToken = default)
    {
        const string genericMessage =
            "If an account exists for this email, password reset instructions have been processed.";

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByPredicateAsync(
            u => u.Email == email && !u.IsDeleted,
            cancellationToken);

        if (user == null || !user.IsActive)
        {
            _logger.LogInformation("Password reset requested for unknown or inactive email (no user enumeration).");
            return new ForgotPasswordResponseDto { Message = genericMessage };
        }

        var rawToken = GeneratePasswordResetToken();
        user.PasswordResetTokenHash = HashResetToken(rawToken);
        var hours = Math.Clamp(_passwordResetOptions.TokenExpiryHours, 1, 72);
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(hours);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password reset token issued for user {UserId}.", user.Id);

        var response = new ForgotPasswordResponseDto { Message = genericMessage };
        if (_passwordResetOptions.ExposeTokenInResponse)
            response.ResetToken = rawToken;

        return response;
    }

    public async Task ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
            throw new InvalidOperationException("Passwords do not match.");

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByPredicateAsync(
            u => u.Email == email && !u.IsDeleted,
            cancellationToken);

        if (user == null
            || string.IsNullOrEmpty(user.PasswordResetTokenHash)
            || !user.PasswordResetTokenExpiry.HasValue)
        {
            throw new InvalidOperationException("Invalid or expired reset request.");
        }

        if (user.PasswordResetTokenExpiry.Value < DateTime.UtcNow)
            throw new InvalidOperationException("Invalid or expired reset request.");

        var presentedHash = HashResetToken(request.Token.Trim());
        if (!FixedTimeEqualsHex(user.PasswordResetTokenHash, presentedHash))
            throw new InvalidOperationException("Invalid or expired reset request.");

        user.PasswordHash = HashPassword(request.NewPassword);
        user.PasswordResetTokenHash = null;
        user.PasswordResetTokenExpiry = null;
        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Password reset completed for user {UserId}.", user.Id);
    }

    private static string GeneratePasswordResetToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string HashResetToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool FixedTimeEqualsHex(string a, string b)
    {
        if (a.Length != b.Length || a.Length != 64)
            return false;
        var result = 0;
        for (var i = 0; i < a.Length; i++)
            result |= char.ToLowerInvariant(a[i]) ^ char.ToLowerInvariant(b[i]);
        return result == 0;
    }

    private async Task<List<string>> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var userRoles = await _userRoleRepository.FindAsync(
            ur => ur.UserId == userId && !ur.IsDeleted,
            cancellationToken);

        var roleIds = userRoles.Select(ur => ur.RoleId).ToList();
        var roles = new List<string>();

        foreach (var roleId in roleIds)
        {
            var role = await _roleRepository.GetByIdAsync(roleId, cancellationToken);
            if (role != null && !role.IsDeleted)
            {
                roles.Add(role.Name);
            }
        }

        return roles;
    }

    /// <summary>Resolve client for user: when user is active, try company path (User → CompanyUsers → Company → Client) first; otherwise use User.ClientId.</summary>
    private async Task<Client?> ResolveClientForUserAsync(User user, CancellationToken cancellationToken)
    {
        if (user.IsActive)
        {
            var fromCompany = await _clientByCompanyQuery.GetClientForUserEmailAsync(user.Email, cancellationToken);
            if (fromCompany != null)
                return await _clientRepository.GetByIdAsync(fromCompany.ClientId, cancellationToken);
        }
        return await GetClientForUserAsync(user, cancellationToken);
    }

    /// <summary>Load client for user (for claims). Logs when user has no client so admins can fix assignment and have user re-login.</summary>
    private async Task<Client?> GetClientForUserAsync(User user, CancellationToken cancellationToken)
    {
        if (!user.ClientId.HasValue)
        {
            _logger.LogWarning("User {Email} has no ClientId. Assign via POST /api/admin/clients/{{clientId}}/users/{{userId}}, then have user log in again.", user.Email);
            return null;
        }
        var client = await _clientRepository.GetByIdAsync(user.ClientId.Value, cancellationToken);
        if (client == null)
            _logger.LogWarning("User {Email} has ClientId {ClientId} but client not found (deleted?). No client_code claim.", user.Email, user.ClientId);
        return client;
    }

    private List<Claim> GenerateClaims(User user, List<string> roles, Client? client)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            new(ClaimTypes.GivenName, user.FirstName),
            new(ClaimTypes.Surname, user.LastName)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var folderName = client?.ClientCode ?? client?.BlobFolderPath;
        if (!string.IsNullOrEmpty(folderName))
            claims.Add(new Claim("client_code", folderName));

        return claims;
    }

    private async Task<UserDto> MapUserToDtoAsync(User user, List<string> roles, Client? client)
    {
        // Branding is a premium entitlement: only actually surfaced when the client is both
        // admin-declared premium AND has a logo uploaded. A non-premium client with a logo on
        // file (e.g. uploaded before a downgrade) still gets default StudioTechBI branding.
        string? logoUrl = null;
        if (client?.IsPremiumSubscriber == true && !string.IsNullOrEmpty(client.LogoBlobPath))
            logoUrl = await _sasUriProvider.GetReadSasUriAsync(client.LogoBlobPath, LogoSasValidFor);

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
            ClientId = client?.Id ?? user.ClientId,
            ClientCode = client?.ClientCode ?? client?.BlobFolderPath,
            // Both null when no logo is configured or the client isn't premium --
            // useClientBranding() falls back to default StudioTechBI branding with no
            // special-casing needed downstream.
            CompanyName = logoUrl != null ? client?.ClientName : null,
            LogoUrl = logoUrl,
            HasReportValidationAddOn = client?.HasReportValidationAddOn ?? false,
            Roles = roles
        };
    }

    public static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
    }

    public static bool VerifyPassword(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            return false;
        }
    }
}
