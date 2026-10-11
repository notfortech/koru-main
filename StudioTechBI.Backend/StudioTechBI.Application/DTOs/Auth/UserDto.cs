using System.Text.Json.Serialization;

namespace StudioTechBI.Application.DTOs.Auth;

public class UserDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;
    [JsonPropertyName("lastName")]
    public string LastName { get; set; } = string.Empty;
    [JsonPropertyName("phoneNumber")]
    public string? PhoneNumber { get; set; }
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }
    /// <summary>Client (folder) ID this user is mapped to. Populated from User.ClientId when user is linked.</summary>
    [JsonPropertyName("clientId")]
    public Guid? ClientId { get; set; }
    /// <summary>Client code e.g. AU-001. Populated from Client.ClientCode when user has ClientId.</summary>
    [JsonPropertyName("clientCode")]
    public string? ClientCode { get; set; }
    /// <summary>White-label company display name (Client.ClientName), only meaningful together
    /// with LogoUrl. Null when the client has no logo configured -- default StudioTechBI branding
    /// applies.</summary>
    [JsonPropertyName("companyName")]
    public string? CompanyName { get; set; }
    /// <summary>Short-lived read SAS URL for the client's uploaded white-label logo, null when
    /// none is configured. Refreshed on every login/token refresh.</summary>
    [JsonPropertyName("logoUrl")]
    public string? LogoUrl { get; set; }
    /// <summary>Whitelabel theme colors (Client.BrandPrimaryColor/BrandSecondaryColor), additive
    /// on top of the logo above -- both null when no logo is configured, same gate as
    /// CompanyName/LogoUrl (see AuthService.MapUserToDtoAsync).</summary>
    [JsonPropertyName("primaryColor")]
    public string? PrimaryColor { get; set; }
    [JsonPropertyName("secondaryColor")]
    public string? SecondaryColor { get; set; }
    /// <summary>Short-lived read SAS URL for the client's uploaded white-label favicon, null when
    /// none is configured. Gated on its own presence only, not tied to the logo/premium gate above
    /// -- a client can have a logo but no favicon uploaded yet.</summary>
    [JsonPropertyName("faviconUrl")]
    public string? FaviconUrl { get; set; }
    /// <summary>Admin-declared entitlement for the paid Report Validation add-on (Client.
    /// HasReportValidationAddOn), a separate subscription line item from branding. Frontend gates
    /// the "Validate Report" button/screen on this plain boolean.</summary>
    [JsonPropertyName("hasReportValidationAddOn")]
    public bool HasReportValidationAddOn { get; set; }
    /// <summary>Admin-declared, temporary restriction (Client.HasLimitedPortalAccess): when true,
    /// the frontend reduces this client's portal to just the "Reports Generated" dashboard card,
    /// hides all AI-credits UI, and hides Profile/Propositions.</summary>
    [JsonPropertyName("hasLimitedPortalAccess")]
    public bool HasLimitedPortalAccess { get; set; }
    [JsonPropertyName("roles")]
    public List<string> Roles { get; set; } = new();
}
