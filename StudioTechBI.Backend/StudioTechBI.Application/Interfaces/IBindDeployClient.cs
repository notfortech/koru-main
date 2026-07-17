using StudioTechBI.Application.DTOs.BindDeploy;

namespace StudioTechBI.Application.Interfaces;

/// <summary>
/// Typed client for stbi-bind-deploy — the credential-isolated service that actually touches
/// Power BI. Deliberately NOT wired into IAiBoundaryAuditService: no LLM call happens on this
/// path, so it isn't an AI-boundary crossing — it's the deterministic data-plane step that
/// happens strictly after S7 (authoring) and S8's validator have already run.
/// </summary>
public interface IBindDeployClient
{
    Task<DeployDatasetResult> DeployDatasetAsync(
        DeployDatasetRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
}
