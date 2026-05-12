namespace StudioTechBI.API.Configuration;

/// <summary>
/// Domain.com.au API integration. Secrets: use appsettings (local only), User Secrets, Key Vault, or
/// Azure App Service settings <c>DomainApi__ClientId</c>, <c>DomainApi__ClientSecret</c> (also <c>DOMAIN_API_*</c> if mapped in Program.cs).
/// </summary>
public class DomainApiSettings
{
    public const string SectionName = "DomainApi";

    /// <summary>Resource API base URL, e.g. https://api.domain.com.au</summary>
    public string BaseUrl { get; set; } = "https://api.domain.com.au";

    /// <summary>OAuth issuer base URL for client credentials (token is not served from the API host).</summary>
    public string AuthBaseUrl { get; set; } = "https://auth.domain.com.au";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Space-separated OAuth scopes from your Domain developer app (required for client_credentials).</summary>
    public string Scope { get; set; } = string.Empty;
}
