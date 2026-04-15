namespace StudioTechBI.Domain.Entities;

public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }

    /// <summary>SHA-256 hex (64 chars) of the raw reset token; null when no reset is pending.</summary>
    public string? PasswordResetTokenHash { get; set; }

    public DateTime? PasswordResetTokenExpiry { get; set; }
    /// <summary>0 = general client (single-tenant login; no client picker). 1 = accountant (may be assigned a client folder).</summary>
    public int UserType { get; set; }

    /// <summary>Client (folder) this user is mapped to; determines which reports they can view/refresh.</summary>
    public Guid? ClientId { get; set; }
    /// <summary>Tenant this user is assigned to (admin-assignable).</summary>
    public Guid? TenantId { get; set; }

    public Client? Client { get; set; }
    public Tenant? Tenant { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<CompanyUser> CompanyUsers { get; set; } = new List<CompanyUser>();
}
