using StudioTechBI.Application.Models;

namespace StudioTechBI.Application.Utilities;

/// <summary>
/// Decides when to call the external InsightsEngine.Api vs. catalog-only copilot behavior.
/// Reversible via <see cref="InsightsEngineOptions.ExternalCopilotAiEnabled"/> and score threshold.
/// </summary>
public static class CopilotExternalAiGate
{
    public static bool ShouldCallExternalInsightsEngine(InsightsEngineOptions options, double bestCatalogMatchScore)
    {
        if (options is null)
            return false;
        if (!options.ExternalCopilotAiEnabled)
            return false;
        if (double.IsNaN(bestCatalogMatchScore) || double.IsInfinity(bestCatalogMatchScore))
            return true;
        return bestCatalogMatchScore < options.MinTemplateMatchScoreToSkipExternalAi;
    }

    /// <summary>Whether to run template mapping refinement against /api/ai/templates/map.</summary>
    public static bool ShouldRefineMappingWithExternalAi(
        InsightsEngineOptions options,
        double bestCatalogMatchScore,
        bool userEnabledRefinementInRequest)
    {
        if (!userEnabledRefinementInRequest)
            return false;
        return ShouldCallExternalInsightsEngine(options, bestCatalogMatchScore);
    }
}
