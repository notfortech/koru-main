namespace StudioTechBI.Application.DTOs.Insight;

/// <summary>Standard response for POST /api/models/{modelId}/select (frontend contract).</summary>
public class SelectModelResponseDto
{
    public string? DatasetId { get; set; }
    public string? ReportId { get; set; }
    public bool Queued { get; set; }
    public Guid? JobId { get; set; }
    public string Message { get; set; } = string.Empty;
}
