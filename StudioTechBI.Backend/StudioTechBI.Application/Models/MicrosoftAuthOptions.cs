namespace StudioTechBI.Application.Models;

/// <summary>Microsoft identity platform (Entra) OAuth for delegated Graph access (e.g. OneDrive).</summary>
public class MicrosoftAuthOptions
{
    public const string SectionName = "MicrosoftAuth";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Tenant id or <c>common</c> for multi-tenant consumer/work accounts.</summary>
    public string TenantId { get; set; } = "common";

    /// <summary>Must exactly match an app registration redirect URI (e.g. https://api.../api/connections/oauth/onedrive/callback).</summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>Optional. After successful token exchange, browser is redirected here (e.g. SPA). If empty, redirects to /swagger.</summary>
    public string? SuccessRedirectUri { get; set; }
}
