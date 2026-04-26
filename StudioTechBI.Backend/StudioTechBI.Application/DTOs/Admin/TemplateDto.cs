namespace StudioTechBI.Application.DTOs.Admin;

public class TemplateDto
{
    public Guid TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string Version { get; set; } = string.Empty;
    public string? BlobPath { get; set; }
    public string? ModelId { get; set; }
    public List<string> RequiredColumns { get; set; } = new();
    public List<string> OptionalColumns { get; set; } = new();
    public DateTime CreatedDate { get; set; }
}
