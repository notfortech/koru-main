using StudioTechBI.Application.DTOs.Templates;

namespace StudioTechBI.Application.DTOs.TemplateRefresh;

/// <summary>
/// Wraps the existing TemplateMatchResponse (unchanged) with one derived field the caller needs
/// to decide whether to rebind immediately or show the column mapping for confirmation first —
/// kept as a wrapper rather than adding a field to TemplateMatchResponse itself.
/// </summary>
public sealed class TemplateRefreshMatchResponse
{
    public TemplateMatchResponse Match { get; set; } = new();

    /// <summary>
    /// True when the top-ranked match's confidence indicates every required column was present
    /// (score = 0.8 * requiredRatio + 0.2 * optionalRatio, so >= 0.8 only happens at
    /// requiredRatio = 1.0) — safe to rebind without a human confirming a column remap first.
    /// </summary>
    public bool ReadyForInstantRefresh { get; set; }
}

/// <summary>Request to POST /api/template-refresh/rebind.</summary>
public sealed record TemplateRefreshRebindRequest(
    string ClientCode,
    bool UseSelectedClient,
    Guid TemplateId,
    string SourceFilePath);

public sealed record TemplateRefreshRebindResponse(
    string WorkspaceId,
    string WorkspaceName,
    string DatasetId,
    string DatasetName,
    bool RefreshTriggered,
    IReadOnlyList<string> Steps,
    string? ReportId = null,
    string? ReportName = null,
    bool ReportCloned = false);

/// <summary>Mirrors stbi-bind-deploy's RebindTemplateRequest — the payload ITemplateRebindClient sends.</summary>
public sealed record TemplateRebindRequest(
    string ClientName,
    string TemplateWorkspaceName,
    string TemplateDatasetName,
    string SourceFilePath,
    string? CapacityId = null,
    string? TemplateReportName = null);

/// <summary>Mirrors stbi-bind-deploy's RebindTemplateResult.</summary>
public sealed record TemplateRebindResult(
    string WorkspaceId,
    string WorkspaceName,
    string DatasetId,
    string DatasetName,
    bool RefreshTriggered,
    List<string> Steps,
    string? ReportId = null,
    string? ReportName = null,
    bool ReportCloned = false);
