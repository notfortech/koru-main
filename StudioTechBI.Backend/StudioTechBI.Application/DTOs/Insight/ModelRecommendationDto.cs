namespace StudioTechBI.Application.DTOs.Insight;

public sealed class ModelRecommendationDto
{
    public Guid ModelId { get; set; }
    public string? TemplateId { get; set; }
    public double Confidence { get; set; }
}

