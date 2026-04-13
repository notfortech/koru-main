namespace StudioTechBI.Application.DTOs.Insight;

public class GenerateModelRequest
{
    /// <summary>Blob path passed to InsightEngine (e.g. AU-001/accounting/created/file.xlsx).</summary>
    public string BlobPath { get; set; } = string.Empty;

    public Guid? ClientId { get; set; }
}
