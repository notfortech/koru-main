using StudioTechBI.Application.DTOs.ReportDesigner;

namespace StudioTechBI.Application.DTOs.BindDeploy;

/// <summary>
/// Mirrors stbi-bind-deploy's DeployDatasetRequest. Reuses ReportDesigner.TmdlFileDto (Path,
/// Content) rather than declaring a second identical record — this is the same file shape that
/// comes back from AuthorTmdlResponse.Files, just handed on to the deploy step unchanged.
/// </summary>
public record DeployDatasetRequest(
    string ClientName,
    string DatasetName,
    List<TmdlFileDto> TmdlFiles,
    string? CapacityId = null);

/// <summary>Mirrors stbi-bind-deploy's DeployDatasetResult.</summary>
public record DeployDatasetResult(
    string WorkspaceId,
    string WorkspaceName,
    string DatasetId,
    string DatasetName,
    bool Created,
    List<string> Steps);
