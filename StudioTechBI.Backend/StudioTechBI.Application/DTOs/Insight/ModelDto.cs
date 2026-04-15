using System.Text.Json.Serialization;

namespace StudioTechBI.Application.DTOs.Insight;

public class ModelDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; }

    /// <summary>Opaque template id from InsightEngine (string or guid-as-string).</summary>
    public string? TemplateId { get; set; }

    public string? Status { get; set; }

    [JsonPropertyName("confidence")]
    public double? Confidence { get; set; }

    [JsonPropertyName("confidenceScore")]
    public double? ConfidenceScore { get; set; }

    public string? MappingJson { get; set; }
    public string? ExcelSchemaJson { get; set; }
    public string? SchemaHash { get; set; }
    public string? ValidatedBlobPath { get; set; }
    public bool? IsFallback { get; set; }

    /// <summary>From active InsightDataset when present (client model list).</summary>
    public string? DatasetId { get; set; }

    /// <summary>From active InsightDataset when present (client model list).</summary>
    public string? ReportId { get; set; }

    public double ResolveConfidence() => Confidence ?? ConfidenceScore ?? 0;
}
