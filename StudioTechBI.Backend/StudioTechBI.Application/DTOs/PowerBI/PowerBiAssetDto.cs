namespace StudioTechBI.Application.DTOs.PowerBI;

public class PowerBiAssetDto
{
    public Guid? ClientId { get; set; }
    public string? ReportType { get; set; }

    public string? WorkspaceId { get; set; }
    public string? DatasetId { get; set; }
    public string? ReportId { get; set; }

    public string? CapacityId { get; set; }

    /// <summary>When this asset was created — the only reliable way to tell apart multiple assets
    /// of the same ReportType for the same client (e.g. repeat Dashboard Template Generator runs),
    /// since PowerBiAsset has no persisted display name.</summary>
    public DateTime? CreatedAt { get; set; }
}

