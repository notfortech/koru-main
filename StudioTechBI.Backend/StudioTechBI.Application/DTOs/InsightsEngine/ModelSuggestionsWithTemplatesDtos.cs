namespace StudioTechBI.Application.DTOs.InsightsEngine;

/// <summary>AI-proposed models from InsightsEngine.Api plus verified template matches from our catalog.</summary>
public sealed class ModelSuggestionsWithTemplatesResponse
{
    public ModelSuggestResponse Suggestions { get; set; } = new();

    /// <summary>Templates from our DB ranked by match to user prompt and detected schema.</summary>
    public IReadOnlyList<VerifiedTemplateMatch> VerifiedTemplates { get; set; } = Array.Empty<VerifiedTemplateMatch>();
}

