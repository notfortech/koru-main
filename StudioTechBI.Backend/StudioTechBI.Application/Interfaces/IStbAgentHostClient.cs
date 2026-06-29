using StudioTechBI.Application.DTOs.Blueprint;

namespace StudioTechBI.Application.Interfaces;

public interface IStbAgentHostClient
{
    /// <summary>
    /// Calls POST /api/blueprints/generate on the STBI AgentHost.
    /// Sends X-Tenant-Id and X-Tenant-Name headers derived from the client record.
    /// </summary>
    Task<StbAgentHostResponseDto> GenerateBlueprintAsync(
        StbAgentHostRequestDto request,
        string tenantId,
        string tenantName,
        CancellationToken ct = default);

    /// <summary>
    /// Proxies GET /api/blueprints/{requestId}/pdf from AgentHost and streams the PDF bytes.
    /// Returns null when the AgentHost returns 404.
    /// </summary>
    Task<Stream?> DownloadPdfAsync(string agentHostPdfUrl, CancellationToken ct = default);
}
