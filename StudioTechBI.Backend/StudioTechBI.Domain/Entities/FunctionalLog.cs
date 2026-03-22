namespace StudioTechBI.Domain.Entities;

public class FunctionalLog
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public Guid? ClientId { get; set; }
    public string? Description { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
