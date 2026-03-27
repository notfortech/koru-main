using System.ComponentModel.DataAnnotations;

namespace StudioTechBI.Application.DTOs.Admin;

public class AdminUserDisableDto
{
    [Required]
    public bool IsActive { get; set; }
}
