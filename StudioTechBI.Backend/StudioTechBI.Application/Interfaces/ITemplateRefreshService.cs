using StudioTechBI.Application.DTOs.TemplateRefresh;
using StudioTechBI.Application.DTOs.Templates;

namespace StudioTechBI.Application.Interfaces;

/// <summary>
/// New, standalone orchestration for the "match a client's schema against the existing template
/// catalog, then rebind the matched template to that client" flow. Does not replace or extend
/// IReportMatchService/ReportDesignerController's AI-generation publish path — this is the
/// matching-only flow the manually-curated template catalog needs (see
/// BLUEPRINT_TEMPLATE_FORMAT.md in stbi_transformers for why template generation stays out of
/// this platform).
/// </summary>
public interface ITemplateRefreshService
{
    Task<TemplateRefreshMatchResponse> MatchAsync(TemplateMatchRequest request, CancellationToken cancellationToken = default);

    Task<TemplateRefreshRebindResponse> RebindAsync(TemplateRefreshRebindRequest request, CancellationToken cancellationToken = default);
}
