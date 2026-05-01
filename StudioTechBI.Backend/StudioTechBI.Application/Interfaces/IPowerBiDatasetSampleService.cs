using StudioTechBI.Application.DTOs.InsightsEngine;
using StudioTechBI.Application.DTOs.PowerBI;

namespace StudioTechBI.Application.Interfaces;

/// <summary>Runs approved DAX against a Power BI dataset to build capped tabular samples for AI insight prompts.</summary>
public interface IPowerBiDatasetSampleService
{
    /// <summary>
    /// Returns empty when disabled, missing ids, or on failure (caller continues with metadata-only insights).
    /// </summary>
    Task<IReadOnlyList<DatasetQueryTableSample>> GetDatasetSamplesAsync(
        PowerBiAssetDto? asset,
        string reportType,
        string activePageName,
        string? impersonatedUserPrincipalName,
        CancellationToken cancellationToken = default);
}
