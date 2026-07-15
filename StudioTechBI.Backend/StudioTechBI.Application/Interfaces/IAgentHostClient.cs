using StudioTechBI.Application.DTOs.Blueprints;
using StudioTechBI.Application.DTOs.Credits;

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

    /// <summary>
    /// Pre-flight credit check against the shared tenant credit ledger. Fails open (returns
    /// Allowed=true, CreditsRemaining=null) on any connectivity error — only an explicit 402
    /// from AgentHost produces Allowed=false. Implementations must not throw.
    /// </summary>
    Task<CreditCheckResult> CheckCreditsAsync(
        Guid tenantId,
        string? tenantName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deducts one request's worth of credits for a completed AI-assisted action outside the
    /// Blueprint pipeline. Call only after the AI call succeeded. Returns null on any failure —
    /// implementations must not throw, since a failed deduction should not fail the request that
    /// already succeeded.
    /// </summary>
    Task<CreditConsumeResult?> ConsumeCreditAsync(
        Guid tenantId,
        string feature,
        string? requestId,
        long executionTimeMs,
        CancellationToken cancellationToken = default);
}
