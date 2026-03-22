using StudioTechBI.Domain.Enums;

namespace StudioTechBI.Domain.Entities;

public class TechnicalLog
{
    public Guid Id { get; set; }
    public string Service { get; set; } = string.Empty;
    public TechnicalLogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
