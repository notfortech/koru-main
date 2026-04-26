using System.Text.Json.Serialization;

namespace StudioTechBI.Application.DTOs.Templates;

public sealed class TemplateMatchRequest
{
    public string? ClientCode { get; set; }
    public string? BlobPath { get; set; }
    public int MaxRows { get; set; } = 100;
    public bool UseSelectedClient { get; set; }

    /// <summary>When true, call the AI refinement endpoint after deterministic ranking.</summary>
    public bool UseAiRefinement { get; set; } = true;
}

public sealed class TemplateMatchResponse
{
    public string ClientCode { get; set; } = string.Empty;
    public string BlobPath { get; set; } = string.Empty;
    public IReadOnlyList<TemplateMatchResult> Templates { get; set; } = Array.Empty<TemplateMatchResult>();
}

public sealed class TemplateMatchResult
{
    public Guid TemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public TemplateMappingPreview? MappingPreview { get; set; }
}

public sealed class TemplateMappingPreview
{
    public IReadOnlyList<TemplateColumnMapping> Mapping { get; set; } = Array.Empty<TemplateColumnMapping>();
    public IReadOnlyList<string> Transformations { get; set; } = Array.Empty<string>();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Notes { get; set; }
}

public sealed class TemplateColumnMapping
{
    public string TemplateColumn { get; set; } = string.Empty;
    public string ClientColumn { get; set; } = string.Empty;
    public string? Transform { get; set; }
}

