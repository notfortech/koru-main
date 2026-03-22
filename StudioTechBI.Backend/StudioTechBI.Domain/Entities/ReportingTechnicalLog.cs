namespace StudioTechBI.Domain.Entities;

/// <summary>Technical log entry in reporting schema (reporting.TechnicalLogs).</summary>
public class ReportingTechnicalLog
{
    public Guid LogId { get; set; }
    public string? Service { get; set; }
    public string? Level { get; set; }
    public string? Message { get; set; }
    public string? StackTrace { get; set; }
    public DateTime? CreatedDate { get; set; }
}
