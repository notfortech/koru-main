namespace StudioTechBI.Application.DTOs.BindDeploy;

/// <summary>Mirrors stbi-bind-deploy's PbipFileDto — the payload IPbipImportClient sends.</summary>
public sealed record PbipFileDto(string RelativePath, string Content);

/// <summary>Mirrors stbi-bind-deploy's RefreshScheduleDto.</summary>
public sealed record RefreshScheduleDto(bool Enabled, List<string> Days, List<string> Times, string TimeZone);

/// <summary>Mirrors stbi-bind-deploy's PbipImportRequest.</summary>
public sealed record PbipImportRequest(
    string ClientName,
    string ReportName,
    string WorkspaceName,
    List<PbipFileDto> Files,
    string? CapacityId = null,
    string? GatewayId = null,
    RefreshScheduleDto? RefreshSchedule = null);

/// <summary>Mirrors stbi-bind-deploy's PbipImportResult.</summary>
public sealed record PbipImportResult(
    string WorkspaceId,
    string WorkspaceName,
    string DatasetId,
    string ReportId,
    bool GatewayBound,
    bool RefreshScheduleSet,
    bool RefreshTriggered,
    List<string> Steps);
