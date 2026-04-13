namespace StudioTechBI.Application.DTOs.Insight;

public class ModelDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public Guid? TemplateId { get; set; }
    public string? Status { get; set; }
    public double? ConfidenceScore { get; set; }
    public string? MappingJson { get; set; }
    public string? ExcelSchemaJson { get; set; }
}
