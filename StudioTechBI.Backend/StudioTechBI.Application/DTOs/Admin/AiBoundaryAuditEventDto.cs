namespace StudioTechBI.Application.DTOs.Admin;

public class AiBoundaryAuditEventDto
{
    public Guid Id { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public string TargetService { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = string.Empty;
    public long? DurationMs { get; set; }
    public int? StatusCode { get; set; }
    public bool? Success { get; set; }
    public string? ErrorSummary { get; set; }
    public Guid? ClientId { get; set; }
    public DateTime Timestamp { get; set; }
}
