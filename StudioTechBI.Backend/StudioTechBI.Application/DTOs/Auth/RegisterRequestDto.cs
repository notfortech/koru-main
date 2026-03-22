using System.ComponentModel.DataAnnotations;

namespace StudioTechBI.Application.DTOs.Auth;

public class RegisterRequestDto
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
    public string Password { get; set; } = string.Empty;

    /// <summary>Optional. If provided, must match Password.</summary>
    public string? ConfirmPassword { get; set; }

    [Required(ErrorMessage = "First name is required")]
    public string FirstName { get; set; } = string.Empty;

    public string? LastName { get; set; }

    public string? PhoneNumber { get; set; }
}
