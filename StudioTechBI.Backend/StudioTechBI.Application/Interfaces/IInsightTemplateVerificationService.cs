using StudioTechBI.Application.DTOs.InsightsEngine;

namespace StudioTechBI.Application.Interfaces;

public interface IInsightTemplateVerificationService
{
    /// <summary>
    /// Scores all non-deleted catalog templates against the user prompt and column names. Does not call external AI.
    /// </summary>
    Task<InsightsWithTemplatesResponse> EnrichWithVerifiedTemplatesAsync(
        TransformSuggestResponse engineResponse,
        string? userPrompt,
        CancellationToken cancellationToken = default);
}
