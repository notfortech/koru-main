using System.Text.Json.Serialization;

namespace StudioTechBI.Application.DTOs.Auth;

public class ForgotPasswordResponseDto
{
    /// <summary>Generic message; same whether or not the email exists.</summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>Only set when <see cref="Models.PasswordResetOptions.ExposeTokenInResponse"/> is true (never in production).</summary>
    [JsonPropertyName("resetToken")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResetToken { get; set; }
}
