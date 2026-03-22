namespace StudioTechBI.Domain.Entities;

/// <summary>Processing job record in reporting schema (reporting.ProcessingJobs).</summary>
public class ReportingProcessingJob
{
    public Guid JobId { get; set; }
    public Guid? ClientId { get; set; }
    public string? FileName { get; set; }
    public string? BlobPath { get; set; }
    public string? Status { get; set; }
    public string? CurrentStep { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
}
