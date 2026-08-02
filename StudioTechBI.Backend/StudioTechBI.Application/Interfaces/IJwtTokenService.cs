using System.Security.Claims;

namespace StudioTechBI.Application.Interfaces;

public interface IJwtTokenService
{
    /// <summary>lifetimeOverride, when supplied, replaces the configured expiration — used for
    /// short-lived, purpose-scoped tokens (e.g. minted for a background Report Validation run's
    /// Playwright session) rather than the user's own long-lived session token.</summary>
    string GenerateAccessToken(IEnumerable<Claim> claims, TimeSpan? lifetimeOverride = null);
    string GenerateRefreshToken();
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
