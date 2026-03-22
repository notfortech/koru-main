namespace StudioTechBI.Application.DTOs.Admin;

public class AdminUserUpdateDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    /// <summary>0 = general client, 1 = accountant (requires ClientId).</summary>
    public int UserType { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? ClientId { get; set; }
}
