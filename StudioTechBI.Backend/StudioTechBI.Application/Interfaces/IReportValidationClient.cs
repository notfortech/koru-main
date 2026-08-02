using StudioTechBI.Application.DTOs.ReportValidation;

namespace StudioTechBI.Application.Interfaces;

/// <summary>Typed client for DashboardAgents.ReportValidationApi — the separate Playwright-based
/// rendering-health service (stbi_transformers repo, its own Linux Azure Container App, same
/// reason DashboardAgents.ReportAgent.Api is separate from koru-main).</summary>
public interface IReportValidationClient
{
    Task<RenderingHealthResponse> RunRenderingHealthAsync(
        Stream fileStream,
        string fileName,
        string? templateId,
        string? filtersJson,
        string authToken,
        string correlationId,
        CancellationToken cancellationToken = default);
}
