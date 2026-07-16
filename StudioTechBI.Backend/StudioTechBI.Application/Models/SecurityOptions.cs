namespace StudioTechBI.Application.Models;

public class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>
    /// Shared secret validating inbound calls to POST /api/internal/ai-boundary-audit-log.
    /// Must match stbi_transformers' own Koru:ServiceApiKey (env var KORU_API_KEY) — that's the
    /// credential it already sends as the X-Service-Api-Key header on every call to this backend.
    /// </summary>
    public string StbiTransformersApiKey { get; set; } = string.Empty;
}
