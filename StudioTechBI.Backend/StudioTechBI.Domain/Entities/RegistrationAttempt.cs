namespace StudioTechBI.Domain.Entities;

public class RegistrationAttempt : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? FailureReason { get; set; }
}
