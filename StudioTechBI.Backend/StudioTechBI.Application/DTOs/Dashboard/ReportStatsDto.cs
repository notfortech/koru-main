namespace StudioTechBI.Application.DTOs.Dashboard;

/// <summary>Everything the client dashboard's "View Report Stats" quick action needs, in one
/// round trip — see ClientPortalReportStatsController.</summary>
public record ReportStatsDto(
    int SavedReportsCount,
    int DeterministicReportsCount,
    int AiAssistedReportsCount,
    int AiCreditsConsumed,
    int? CreditsRemaining,
    bool IsUnlimited,
    DateTimeOffset? ResetDate);
