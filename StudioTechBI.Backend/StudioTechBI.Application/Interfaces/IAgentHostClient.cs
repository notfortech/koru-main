using StudioTechBI.Application.DTOs.Blueprints;

namespace StudioTechBI.Application.Interfaces;

/// <summary>
/// Low-level HTTP client for STBI-AgentHost. Koru must never know about AI models
/// or prompt engineering — this interface is the only integration point.
/// </summary>
public interface IAgentHostClient
{
    Task<BlueprintGenerationResponse> GenerateBlueprintAsync(
        GenerateBlueprintRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pings the AgentHost health endpoint. Returns true if the service reports healthy.
    /// Implementations must not throw — return false on any connectivity failure.
    /// </summary>
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads the PDF bytes from an AgentHost-hosted PDF URL (as returned in
    /// BlueprintGenerationResponse.PdfDownloadUrl). Returns null on any failure or
    /// non-success response — implementations must not throw.
    /// </summary>
    Task<byte[]?> DownloadPdfAsync(string pdfUrl, CancellationToken cancellationToken = default);
}
