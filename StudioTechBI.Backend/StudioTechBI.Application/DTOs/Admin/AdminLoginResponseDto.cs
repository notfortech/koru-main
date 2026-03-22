namespace StudioTechBI.Application.DTOs.Admin;

public class AdminLoginResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public AdminMeDto Admin { get; set; } = null!;
}
