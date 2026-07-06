namespace StudioTechBI.Application.Models;

public class ReportDesignerOptions
{
    public const string SectionName = "ReportDesigner";

    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 120;
}
