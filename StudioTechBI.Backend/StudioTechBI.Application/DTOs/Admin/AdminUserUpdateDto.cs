using System.ComponentModel.DataAnnotations;

namespace StudioTechBI.Application.DTOs.Admin;

public class AdminUserUpdateDto
{
    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }
    /// <summary>0 = general client, 1 = accountant (requires ClientId).</summary>
    [Range(0, 1)]
    public int UserType { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? ClientId { get; set; }
}
