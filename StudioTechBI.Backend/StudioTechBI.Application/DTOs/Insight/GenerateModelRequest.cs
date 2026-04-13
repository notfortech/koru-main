namespace StudioTechBI.Application.DTOs.Insight;

public class GenerateModelRequest
{
    /// <summary>Full blob path where the complete file lives (orchestration / Python pipeline). Engine must not write here.</summary>
    public string BlobPath { get; set; } = string.Empty;

    public Guid? ClientId { get; set; }

    /// <summary>SHA-256 Base64 hash of ordered column names when source is CSV.</summary>
    public string? SchemaHash { get; set; }

    public List<string>? SchemaColumns { get; set; }

    /// <summary>Up to 100 sample rows (string cells) sent to InsightEngine instead of raw file bytes.</summary>
    public List<Dictionary<string, string>>? PreviewRows { get; set; }
}
