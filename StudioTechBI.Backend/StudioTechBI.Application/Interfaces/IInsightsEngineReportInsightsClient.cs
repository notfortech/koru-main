using StudioTechBI.Application.DTOs.InsightsEngine;

namespace StudioTechBI.Application.Interfaces;

public interface IInsightsEngineReportInsightsClient
{
    Task<ReportPageInsightsResponse> GetInsightsFromMetadataAsync(
        ReportPageInsightsRequest request,
        CancellationToken ct = default);
}

