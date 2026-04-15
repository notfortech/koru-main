namespace StudioTechBI.Application.DTOs.Connectors;

/// <summary>Safe list item (no tokens).</summary>
public class DataConnectionSummaryDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
}
