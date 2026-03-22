namespace StudioTechBI.Application.DTOs.Admin;

public class ClientCreateDto
{
    /// <summary>Folder key for blob paths and report API (e.g. AU-001). Required; must be unique.</summary>
    public string ClientCode { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string? TemplateVersion { get; set; }
    public string? PowerBIWorkspaceId { get; set; }
    public string? PowerBIDatasetId { get; set; }
    public string? PowerBIReportId { get; set; }
}
