namespace StudioTechBI.Application.DTOs.Admin;

/// <summary>
/// Payload stbi_transformers posts to /api/internal/ai-boundary-audit-log for its own
/// AI-boundary events (koru-main -> stbi_transformers -> Anthropic/OpenAI). Joined to
/// koru-main's own rows for the same call via CorrelationId, not by matching Operation names.
/// </summary>
public class AiBoundaryAuditEventIngestRequest
{
    public string CorrelationId { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;

    /// <summary>"Sent" | "Received" | "Failed" — dispatches to the matching IAiBoundaryAuditService method.</summary>
    public string Phase { get; set; } = string.Empty;

    /// <summary>"Anthropic" | "OpenAI".</summary>
    public string TargetService { get; set; } = string.Empty;

    public string? MetadataJson { get; set; }
    public long? DurationMs { get; set; }
    public int? StatusCode { get; set; }
    public string? ErrorSummary { get; set; }
    public Guid? ClientId { get; set; }
}
