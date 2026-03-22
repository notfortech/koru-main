namespace StudioTechBI.Application.DTOs.Admin;

public class TechnicalLogDto
{
    public Guid LogId { get; set; }
    public string Service { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public DateTime Timestamp { get; set; }
}
