using StudioTechBI.Application.DTOs.TemplateRefresh;

namespace StudioTechBI.Application.Interfaces;

/// <summary>
/// Typed client for stbi-bind-deploy's new POST /api/templates/rebind endpoint. Deliberately a
/// separate interface from IBindDeployClient — that client only ever creates schema-only Push
/// datasets from TMDL; this one clones an already-published template dataset into a client's
/// workspace and points it at their data. Same target service, different capability.
/// </summary>
public interface ITemplateRebindClient
{
    Task<TemplateRebindResult> RebindAsync(
        TemplateRebindRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
}
