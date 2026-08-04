namespace StudioTechBI.Domain.Entities;

public static class SavedReportSourceTypes
{
    /// <summary>A self-serve HTML report the client generated and explicitly chose to save.</summary>
    public const string GeneratedHtml = "GeneratedHtml";

    /// <summary>A bespoke Power BI report an analyst built to fulfill a CustomReportRequest,
    /// pointing at an existing PowerBiAsset row rather than a blob.</summary>
    public const string PowerBiRequestFulfilled = "PowerBiRequestFulfilled";
}
