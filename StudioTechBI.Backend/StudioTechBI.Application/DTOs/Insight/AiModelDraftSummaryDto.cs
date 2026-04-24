namespace StudioTechBI.Application.DTOs.Insight;

/// <summary>UI-safe summary of an AI model draft. Never includes TOM.</summary>
public sealed class AiModelDraftSummaryDto
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string? TemplateId { get; set; }
    public double Confidence { get; set; }
    public List<string> Transformations { get; set; } = new();
    public List<RelationshipDto> Relationships { get; set; } = new();
    public string Status { get; set; } = string.Empty;
}

