namespace StudioTechBI.Application.DTOs.Insight;

/// <summary>Lightweight request for model recommendations based on a small preview sample.</summary>
public sealed class SampleRequest
{
    public Guid ClientId { get; set; }

    /// <summary>Full blob path where the raw file was uploaded.</summary>
    public string? BlobPath { get; set; }

    public List<string> Columns { get; set; } = new();

    /// <summary>Up to 100 rows of sample data (stringy cells) for AI suggestion.</summary>
    public List<Dictionary<string, object>> SampleRows { get; set; } = new();
}

