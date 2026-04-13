namespace StudioTechBI.Domain.Entities;

/// <summary>AI-generated model candidate for a client (InsightEngine).</summary>
public class InsightModel : BaseEntity
{
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;

    public string? MappingJson { get; set; }
    public string? ExcelSchemaJson { get; set; }
    public Guid? TemplateId { get; set; }
    public string Status { get; set; } = "Pending";
    public double? ConfidenceScore { get; set; }

    public ICollection<InsightDataset> Datasets { get; set; } = new List<InsightDataset>();
}
