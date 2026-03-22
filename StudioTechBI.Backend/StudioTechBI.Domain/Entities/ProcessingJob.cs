using StudioTechBI.Domain.Enums;

namespace StudioTechBI.Domain.Entities;

public class ProcessingJob : BaseEntity
{
    public Guid ClientId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public JobStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public Client Client { get; set; } = null!;
}
