namespace StudioTechBI.Application.Interfaces;

/// <summary>Writes Power BI asset rows to reporting.PowerBIAssets — the table GetAllActiveAssetsForClientAsync reads to populate the reports list screen.</summary>
public interface IPowerBiAssetWriter
{
    Task WriteAsync(
        Guid? clientId,
        Guid? templateId,
        string workspaceId,
        string datasetId,
        string? reportId,
        string reportType,
        CancellationToken cancellationToken = default);
}
