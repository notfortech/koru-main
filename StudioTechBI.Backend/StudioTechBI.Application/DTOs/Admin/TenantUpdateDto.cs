using System.ComponentModel.DataAnnotations;

namespace StudioTechBI.Application.DTOs.Admin;

public class TenantUpdateDto
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string? Country { get; set; }
}
