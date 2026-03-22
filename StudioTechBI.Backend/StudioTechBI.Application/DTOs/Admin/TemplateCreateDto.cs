namespace StudioTechBI.Application.DTOs.Admin;

public class TemplateCreateDto
{
    public string TemplateName { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string Version { get; set; } = string.Empty;
}
