namespace StudioTechBI.Domain.Entities;

/// <summary>Power BI dataset/report produced after a model is selected (InsightEngine orchestrator).</summary>
public class InsightDataset : BaseEntity
{
    public Guid ModelId { get; set; }
    public InsightModel Model { get; set; } = null!;

    public string PowerBIDatasetId { get; set; } = string.Empty;
    public string ReportId { get; set; } = string.Empty;
}
