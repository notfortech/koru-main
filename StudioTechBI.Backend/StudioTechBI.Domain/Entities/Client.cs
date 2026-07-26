namespace StudioTechBI.Domain.Entities;

public class Client : BaseEntity
{
    /// <summary>Folder key used in blob paths and report API (e.g. AU-001).</summary>
    public string? ClientCode { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string? BlobFolderPath { get; set; }
    public string? TemplateVersion { get; set; }

    /// <summary>Blob path to this client's uploaded white-label logo (e.g.
    /// "{clientId}/branding/logo.png"), null when the client uses default StudioTechBI branding.
    /// Set/cleared via AdminClientsController's logo upload/delete endpoints.</summary>
    public string? LogoBlobPath { get; set; }

    /// <summary>Admin-declared entitlement: white-label branding only renders for this client's
    /// users when this is true AND LogoBlobPath is set (see AuthService.MapUserToDtoAsync). Not
    /// derived from the live subscription/credit system — an admin sets this explicitly when they
    /// upgrade a client to a plan that includes white-labeling.</summary>
    public bool IsPremiumSubscriber { get; set; } = false;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // Power BI identifiers are now sourced from reporting.PowerBiAssets (not stored in dbo.Clients).
    // Keep these properties for backward compatibility with existing DTOs/code, but do not map them in EF.
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? PowerBIWorkspaceId { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? PowerBIDatasetId { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? PowerBIReportId { get; set; }

    public ICollection<ProcessingJob> ProcessingJobs { get; set; } = new List<ProcessingJob>();
    public ICollection<InsightModel> InsightModels { get; set; } = new List<InsightModel>();
    public ICollection<DataConnection> DataConnections { get; set; } = new List<DataConnection>();
}
