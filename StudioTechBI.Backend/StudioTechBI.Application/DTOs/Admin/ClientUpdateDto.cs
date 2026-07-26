using System.ComponentModel.DataAnnotations;

namespace StudioTechBI.Application.DTOs.Admin;

public class ClientUpdateDto
{
    public string? ClientCode { get; set; }
    [Required]
    [StringLength(200)]
    public string ClientName { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string? TemplateVersion { get; set; }
    public bool IsPremiumSubscriber { get; set; }
    public bool IsActive { get; set; }
    public string? PowerBIWorkspaceId { get; set; }
    public string? PowerBIDatasetId { get; set; }
    public string? PowerBIReportId { get; set; }
}
