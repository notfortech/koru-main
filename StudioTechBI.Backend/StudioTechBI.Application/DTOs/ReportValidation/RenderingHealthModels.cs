namespace StudioTechBI.Application.DTOs.ReportValidation;

/// <summary>Response contract from DashboardAgents.ReportValidationApi's
/// POST /api/validations/rendering-health.</summary>
public record RenderingHealthResponse(
    bool Success,
    List<RenderingHealthCheckResult> Checks,
    long DurationMs,
    string? ErrorMessage = null);

public record RenderingHealthCheckResult(
    string Name,
    string Status,
    string? Detail,
    List<string>? Evidence = null);
