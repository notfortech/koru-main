using StudioTechBI.Application.DTOs.ReportGenerator;

namespace StudioTechBI.Application.Interfaces;

/// <summary>
/// Typed client for DashboardAgents.ReportAgent.Api — the deterministic,
/// no-AI report engine. Sends the actual connected file (real data); this is
/// the one integration in the platform that intentionally does NOT route
/// through an AI boundary, unlike IReportDesignerClient/IAgentHostClient.
/// </summary>
public interface IReportGeneratorClient
{
    Task<List<ReportTemplateDto>> ListTemplatesAsync(string correlationId, CancellationToken cancellationToken = default);

    Task<GeneratedReportDto> GenerateReportAsync(
        Stream fileStream,
        string fileName,
        string? templateId,
        string correlationId,
        CancellationToken cancellationToken = default);
}
