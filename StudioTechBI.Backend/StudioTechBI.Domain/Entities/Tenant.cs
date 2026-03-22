namespace StudioTechBI.Domain.Entities;

public class Tenant : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string? Country { get; set; }
    public bool IsActive { get; set; } = true;
}
