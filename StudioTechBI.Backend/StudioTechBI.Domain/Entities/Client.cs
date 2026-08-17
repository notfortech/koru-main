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

    /// <summary>Admin-declared entitlement for the paid Report Validation add-on — a separate
    /// subscription line item from IsPremiumSubscriber (branding). Projected onto
    /// UserDto.HasReportValidationAddOn at login/refresh (see AuthService.MapUserToDtoAsync) so
    /// the frontend can gate the "Validate Report" button/screen with a plain boolean.</summary>
    public bool HasReportValidationAddOn { get; set; } = false;

    /// <summary>Admin-declared restriction (temporary, toggled per-client from the admin Client
    /// Details screen): when true, this client's portal shows a reduced UI -- only the "Reports
    /// Generated" dashboard card, no AI-credits UI anywhere, and the Profile/Propositions screens
    /// are hidden from navigation and direct URL access. Defaults false for every client (existing
    /// and newly created) -- an admin opts a specific client in explicitly, never automatic.
    /// Projected onto UserDto.HasLimitedPortalAccess at login/refresh (see
    /// AuthService.MapUserToDtoAsync).</summary>
    public bool HasLimitedPortalAccess { get; set; } = false;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Interim, locally-owned AI credit balance (see LocalCreditLedgerService) — used
    /// while AgentHost's real plan-based ledger is still bypassed (CreditsOptions.BypassEnabled).
    /// Decremented by AI-consuming actions (report model generation, "Ask AI Assistant"), topped
    /// up when an admin marks a CreditPurchaseRequest paid. Default mirrors the current bypass
    /// constant so existing clients don't see a perceived drop the moment this ships.</summary>
    public int AiCreditsRemaining { get; set; } = 1000;

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
