namespace StudioTechBI.Application.DTOs.Insight;

public sealed class AiModelResponse
{
    public string ModelId { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public double Confidence { get; set; }

    public List<string> Transformations { get; set; } = new();
    public List<RelationshipDto> Relationships { get; set; } = new();

    /// <summary>Tabular Object Model JSON. Stored server-side only.</summary>
    public string TomJson { get; set; } = string.Empty;
}

public sealed class RelationshipDto
{
    public string FromTable { get; set; } = string.Empty;
    public string FromColumn { get; set; } = string.Empty;
    public string ToTable { get; set; } = string.Empty;
    public string ToColumn { get; set; } = string.Empty;
    public string? Cardinality { get; set; }
}

