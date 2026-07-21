using StudioTechBI.Application.DTOs.BindDeploy;

namespace StudioTechBI.Application.Interfaces;

/// <summary>
/// Typed client for stbi-bind-deploy's new POST /api/deployments/pbip-import endpoint.
/// Deliberately separate from IBindDeployClient (schema-only Push dataset) and
/// ITemplateRebindClient (clone an existing published template) — this one publishes a
/// freshly-generated PBIP file set (TMDL + report.json + .platform) as a real dataset+report.
/// </summary>
public interface IPbipImportClient
{
    Task<PbipImportResult> ImportAsync(
        PbipImportRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
}
