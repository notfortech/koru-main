namespace StudioTechBI.Application.DTOs.Admin;

public class ProcessingJobDto
{
    public Guid JobId { get; set; }
    public Guid ClientId { get; set; }
    public string? ClientName { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedDate { get; set; }
}
