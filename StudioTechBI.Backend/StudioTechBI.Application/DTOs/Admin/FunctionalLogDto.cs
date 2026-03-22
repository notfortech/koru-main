namespace StudioTechBI.Application.DTOs.Admin;

public class FunctionalLogDto
{
    public Guid LogId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public Guid? ClientId { get; set; }
    public string? Description { get; set; }
    public DateTime Timestamp { get; set; }
}
