namespace StudioTechBI.Application.DTOs.Insight;

public class OrchestratorResultDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? PowerBIDatasetId { get; set; }
    public string? ReportId { get; set; }
}
