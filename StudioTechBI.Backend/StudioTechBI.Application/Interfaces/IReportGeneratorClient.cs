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
        string? filtersJson,
        string? htmlTemplateId,
        string correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>Full ranked HTML template candidate list (profiling + matching only, no KPI/chart
    /// computation) — backs the AI-assisted "Verify Template Match" flow.</summary>
    Task<List<HtmlTemplateCandidateDto>> MatchHtmlTemplateAsync(
        Stream fileStream,
        string fileName,
        string correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>Pushes the full, current set of HTML template manifests (synced from blob by
    /// HtmlTemplateRegistrySyncService) to ReportAgent.Api's local matching cache — this process
    /// has no outbound network access of its own, so its manifest registry is kept warm by push,
    /// never pull.</summary>
    Task PushHtmlTemplateRegistryAsync(
        List<HtmlTemplateManifestPushDto> manifests,
        string correlationId,
        CancellationToken cancellationToken = default);
}
