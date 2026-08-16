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

    /// <summary>Large-file path: sends a short-lived blob read-SAS URL instead of the raw file
    /// bytes, so the app tier never buffers the file a second time on this internal hop. Used by
    /// ReportGenerationJobBackgroundService for jobs that went through the direct-to-blob upload
    /// flow; the small/synchronous path keeps using GenerateReportAsync unchanged.</summary>
    Task<GeneratedReportDto> GenerateReportFromUrlAsync(
        string fileUrl,
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

    /// <summary>Profiles a reference dataset's columns (role + role-gate counts) using the same
    /// profiler the deterministic HTML template matcher's role gate runs at real match time —
    /// backs the admin "upload reference dataset" flow's manifest derivation.</summary>
    Task<ColumnProfileResultDto> ProfileColumnsAsync(
        Stream fileStream,
        string fileName,
        string correlationId,
        CancellationToken cancellationToken = default);
}
