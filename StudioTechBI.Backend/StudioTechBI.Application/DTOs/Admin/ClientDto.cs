namespace StudioTechBI.Application.DTOs.Admin;

public class ClientDto
{
    public Guid ClientId { get; set; }
    /// <summary>Folder key used in blob paths and report API (e.g. AU-001).</summary>
    public string? ClientCode { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string? BlobFolderPath { get; set; }
    public string? TemplateVersion { get; set; }

    /// <summary>Short-lived read SAS URL for the client's uploaded white-label logo, null when
    /// no logo is set (default StudioTechBI branding applies).</summary>
    public string? LogoUrl { get; set; }

    /// <summary>Admin-declared entitlement — branding only actually renders for this client's
    /// users when this is true AND a logo is set.</summary>
    public bool IsPremiumSubscriber { get; set; }

    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? PowerBIWorkspaceId { get; set; }
    public string? PowerBIDatasetId { get; set; }
    public string? PowerBIReportId { get; set; }
}
