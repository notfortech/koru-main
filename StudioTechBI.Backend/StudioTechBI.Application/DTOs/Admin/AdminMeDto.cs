namespace StudioTechBI.Application.DTOs.Admin;

public class AdminMeDto
{
    public Guid AdminId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
