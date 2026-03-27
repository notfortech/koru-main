using System.ComponentModel.DataAnnotations;

namespace StudioTechBI.Application.DTOs.Admin;

public class TemplateCreateDto
{
    [Required]
    [StringLength(200)]
    public string TemplateName { get; set; } = string.Empty;
    public string? Industry { get; set; }

    [Required]
    [StringLength(32)]
    public string Version { get; set; } = string.Empty;
}
