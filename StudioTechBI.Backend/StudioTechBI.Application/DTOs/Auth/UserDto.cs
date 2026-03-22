using System.Text.Json.Serialization;

namespace StudioTechBI.Application.DTOs.Auth;

public class UserDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;
    [JsonPropertyName("lastName")]
    public string LastName { get; set; } = string.Empty;
    [JsonPropertyName("phoneNumber")]
    public string? PhoneNumber { get; set; }
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }
    /// <summary>Client (folder) ID this user is mapped to. Populated from User.ClientId when user is linked.</summary>
    [JsonPropertyName("clientId")]
    public Guid? ClientId { get; set; }
    /// <summary>Client code e.g. AU-001. Populated from Client.ClientCode when user has ClientId.</summary>
    [JsonPropertyName("clientCode")]
    public string? ClientCode { get; set; }
    [JsonPropertyName("roles")]
    public List<string> Roles { get; set; } = new();
}
