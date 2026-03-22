using System.Text.Json.Serialization;

namespace StudioTechBI.Application.DTOs.Auth;

public class LoginResponseDto
{
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; } = string.Empty;
    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; } = string.Empty;
    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }
    [JsonPropertyName("user")]
    public UserDto User { get; set; } = null!;
    /// <summary>Client (folder) ID this user is linked to. Same as user.clientId; included at top level for convenience.</summary>
    [JsonPropertyName("clientId")]
    public Guid? ClientId { get; set; }
    /// <summary>Client code e.g. AU-001. Same as user.clientCode; included at top level for convenience.</summary>
    [JsonPropertyName("clientCode")]
    public string? ClientCode { get; set; }
}
