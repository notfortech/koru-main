namespace StudioTechBI.Application.Models;

public class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    /// <summary>How long the reset token remains valid.</summary>
    public int TokenExpiryHours { get; set; } = 1;

    /// <summary>
    /// When true, POST forgot-password includes the raw token in the JSON response (local/Swagger only).
    /// Must be false in production; use email delivery instead.
    /// </summary>
    public bool ExposeTokenInResponse { get; set; }
}
