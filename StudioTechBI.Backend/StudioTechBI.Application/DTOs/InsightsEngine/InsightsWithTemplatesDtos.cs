using StudioTechBI.Application.DTOs.Admin;

namespace StudioTechBI.Application.DTOs.InsightsEngine;

/// <summary>
/// Raw InsightsEngine transformation output plus template catalog options verified/scored in-app.
/// </summary>
public sealed class InsightsWithTemplatesResponse
{
    public TransformSuggestResponse Insights { get; set; } = new();

    /// <summary>Templates from our DB ranked by match to user prompt and column names (never invent IDs).</summary>
    public IReadOnlyList<VerifiedTemplateMatch> VerifiedTemplates { get; set; } = Array.Empty<VerifiedTemplateMatch>();
}

public sealed class VerifiedTemplateMatch
{
    public TemplateDto Template { get; set; } = null!;

    /// <summary>0..1; higher = stronger match to prompt/columns against template name/industry.</summary>
    public double MatchScore { get; set; }

    public IReadOnlyList<string> MatchReasons { get; set; } = Array.Empty<string>();
}
