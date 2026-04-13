namespace StudioTechBI.Application.DTOs.PowerBI;

public class PowerBiEmbedTokenResult
{
    public string AccessToken { get; set; } = string.Empty;
    public string EmbedUrl { get; set; } = string.Empty;
    public string ReportId { get; set; } = string.Empty;
    public string DatasetId { get; set; } = string.Empty;
}
