namespace StudioTechBI.Application.DTOs.Admin;

public class TemplateDto
{
    public Guid TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string Version { get; set; } = string.Empty;
    public string? BlobPath { get; set; }
    public DateTime CreatedDate { get; set; }
}
