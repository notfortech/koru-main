using System;

namespace StudioTechBI.Domain.Entities;

public class PowerBiAsset
{
    public Guid AssetId { get; set; }

    public Guid? TenantId { get; set; }
    public Guid? ClientId { get; set; }

    // reporting.PowerBiAssets table currently does not expose a Name column (based on your screenshot),
    // so keep this field unmapped to avoid "Invalid column name 'Name'".
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? Name { get; set; }

    public string? WorkspaceId { get; set; }
    public string? DatasetId { get; set; }
    public string? ReportId { get; set; }

    // Some environments may store a GUID; others may store an opaque string identifier (e.g. template name/code used for routing).
    public string? TemplateId { get; set; }
    public string? CapacityId { get; set; }

    public string? ReportType { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

