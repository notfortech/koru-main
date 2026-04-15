namespace StudioTechBI.Application.Models;

/// <summary>
/// Google OAuth for delegated Google Drive access.
/// Production: set <c>GOOGLE_AUTH_CLIENT_ID</c>, <c>GOOGLE_AUTH_CLIENT_SECRET</c>, <c>GOOGLE_AUTH_REDIRECT_URI</c>, <c>GOOGLE_AUTH_SUCCESS_REDIRECT_URI</c> on the host (Azure App Service application settings).
/// Fallback: <c>GoogleAuth</c> section in appsettings when env vars are absent.
/// </summary>
public class GoogleAuthOptions
{
    public const string SectionName = "GoogleAuth";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Must exactly match an OAuth client redirect URI (e.g. API <c>/api/connections/oauth/google/callback</c> on HTTPS).</summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>After successful token exchange, browser is redirected here with <c>?connected=google</c> (HTTPS in production).</summary>
    public string? SuccessRedirectUri { get; set; }

    public bool IsAuthorizationConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(RedirectUri);
}

