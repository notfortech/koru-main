using System.ComponentModel.DataAnnotations;

namespace StudioTechBI.Application.DTOs.Admin;

public class ClientCreateDto
{
    /// <summary>Folder key for blob paths and report API (e.g. AU-001). Required; must be unique.</summary>
    [Required]
    [StringLength(32)]
    public string ClientCode { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string ClientName { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string? TemplateVersion { get; set; }
    public string? PowerBIWorkspaceId { get; set; }
    public string? PowerBIDatasetId { get; set; }
    public string? PowerBIReportId { get; set; }
}
