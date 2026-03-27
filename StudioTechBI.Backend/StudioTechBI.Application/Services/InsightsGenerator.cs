using StudioTechBI.Application.DTOs.AI;

namespace StudioTechBI.Application.Services;

public class AllInsightsService
{
    private readonly PowerBIService _powerBIService;
    private readonly AIInsightsService _aiInsightsService;

    public AllInsightsService(
        PowerBIService powerBIService,
        AIInsightsService aiInsightsService)
    {
        _powerBIService = powerBIService;
        _aiInsightsService = aiInsightsService;
    }

    public async Task<string> GetInsights(
        string periodType,
        string? period,
        string? clientCode)
    {
        // 🔹 Step 1: Get summary data from Power BI layer
        var summary = await _powerBIService.GetDashboardSummary(
            periodType,
            period,
            clientCode);

        // 🔹 Step 2: Send to AI
        var insights = await _aiInsightsService.GenerateInsights(summary);

        return insights;
    }
}