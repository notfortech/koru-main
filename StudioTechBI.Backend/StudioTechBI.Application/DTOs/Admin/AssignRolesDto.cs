using System.ComponentModel.DataAnnotations;

namespace StudioTechBI.Application.DTOs.Admin;

public class AssignRolesDto
{
    [Required]
    [MinLength(1)]
    public List<Guid> RoleIds { get; set; } = new();
}
