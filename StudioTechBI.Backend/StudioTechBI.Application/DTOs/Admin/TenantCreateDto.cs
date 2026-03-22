namespace StudioTechBI.Application.DTOs.Admin;

public class TenantCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string? Country { get; set; }
}
