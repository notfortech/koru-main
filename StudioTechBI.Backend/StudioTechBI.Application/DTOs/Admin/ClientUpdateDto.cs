using System.ComponentModel.DataAnnotations;

namespace StudioTechBI.Application.DTOs.Admin;

public class ClientUpdateDto
{
    private const string HexColorPattern = "^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$";

    public string? ClientCode { get; set; }
    [Required]
    [StringLength(200)]
    public string ClientName { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string? TemplateVersion { get; set; }
    public bool IsPremiumSubscriber { get; set; }
    public bool HasReportValidationAddOn { get; set; }
    public bool HasLimitedPortalAccess { get; set; }
    public bool IsActive { get; set; }
    public string? PowerBIWorkspaceId { get; set; }
    public string? PowerBIDatasetId { get; set; }
    public string? PowerBIReportId { get; set; }

    /// <summary>Whitelabel theme colors (e.g. "#1A2B3C"), additive on top of the client's logo —
    /// see Client.BrandPrimaryColor/BrandSecondaryColor. Null clears the override.</summary>
    [RegularExpression(HexColorPattern, ErrorMessage = "BrandPrimaryColor must be a hex color like #1A2B3C.")]
    public string? BrandPrimaryColor { get; set; }
    [RegularExpression(HexColorPattern, ErrorMessage = "BrandSecondaryColor must be a hex color like #1A2B3C.")]
    public string? BrandSecondaryColor { get; set; }
}
