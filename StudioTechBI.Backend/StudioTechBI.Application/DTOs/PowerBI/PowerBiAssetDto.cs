namespace StudioTechBI.Application.DTOs.PowerBI;

public class PowerBiAssetDto
{
    public Guid? ClientId { get; set; }
    public string? ReportType { get; set; }

    public string? WorkspaceId { get; set; }
    public string? DatasetId { get; set; }
    public string? ReportId { get; set; }

    public string? CapacityId { get; set; }
}

