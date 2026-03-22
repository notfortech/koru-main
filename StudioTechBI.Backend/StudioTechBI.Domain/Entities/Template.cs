namespace StudioTechBI.Domain.Entities;

public class Template : BaseEntity
{
    public string TemplateName { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string Version { get; set; } = string.Empty;
    public string? BlobPath { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
