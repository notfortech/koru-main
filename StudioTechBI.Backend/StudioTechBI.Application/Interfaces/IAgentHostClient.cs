using StudioTechBI.Application.DTOs.Blueprints;
using StudioTechBI.Application.DTOs.Credits;
using StudioTechBI.Application.DTOs.VisualPlan;

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
    /// Calls AgentHost's visual-plan generator (POST /api/visual-plan/generate, aliased
    /// /api/visual-plans): given column structure + a few sample rows per table (never full data)
    /// plus an already-generated star schema, returns a proposed chart spec array. Sibling to
    /// GenerateBlueprintAsync — same auth/timeout conventions via the shared HttpClient, same
    /// correlation-id header, same "koru-main has no AI logic of its own" boundary. Backs
    /// ReportGeneratorController's internal/QA-only "generate-preview" endpoint — never reachable
    /// through the disabled mode=ai toggle on /api/report-generator/generate.
    /// </summary>
    Task<List<VisualPlanChartSpecDto>> GenerateVisualPlanAsync(
        VisualPlanGenerationRequest request,
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

    /// <summary>
    /// Grants additional credits to a tenant via AgentHost's admin surface — used to fulfil an
    /// admin-approved credit purchase. Requires AgentHostOptions.AdminApiKey. Returns null on any
    /// failure (network, bad admin key, tenant not found) — implementations must not throw;
    /// callers should treat a null result as "the grant did not happen" and surface that clearly,
    /// since unlike ConsumeCreditAsync this is not a fire-and-forget side effect of an already-
    /// successful action.
    /// </summary>
    Task<CreditGrantResult?> GrantCreditsAsync(
        Guid tenantId,
        int credits,
        string reason,
        string? referenceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sums a tenant's credits consumed for one feature (e.g. "report-model-generation"), for
    /// surfacing on the client's Report Stats. Returns a zeroed summary (never null/throws) on any
    /// failure, since a stats widget failing to load a sub-figure shouldn't block the rest of it.
    /// </summary>
    Task<CreditUsageSummary> GetUsageSummaryAsync(
        Guid tenantId,
        string feature,
        CancellationToken cancellationToken = default);
}
