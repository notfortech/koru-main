namespace StudioTechBI.Application.DTOs.Admin;

public class AdminUserCreateDto
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    /// <summary>0 = general client, 1 = accountant (requires ClientId).</summary>
    public int UserType { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? ClientId { get; set; }
    public List<Guid>? RoleIds { get; set; }
}
