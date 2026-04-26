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

    public string? ModelId { get; set; }

    /// <summary>Required columns (names as they appear in client data).</summary>
    public List<string> RequiredColumns { get; set; } = new();

    /// <summary>Optional columns used to boost match score.</summary>
    public List<string> OptionalColumns { get; set; } = new();
}
