using System.ComponentModel.DataAnnotations;

namespace StudioTechBI.Application.DTOs.Admin;

public class TenantStatusDto
{
    [Required]
    public bool IsActive { get; set; }
}
