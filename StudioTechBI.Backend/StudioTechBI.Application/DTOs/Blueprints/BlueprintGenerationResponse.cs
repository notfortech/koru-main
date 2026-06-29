using System.Text.Json;

namespace StudioTechBI.Application.DTOs.Blueprints;

/// <summary>
/// Raw response received from STBI-AgentHost /api/blueprints/generate.
/// Koru validates this, persists the artefacts, and never exposes it directly to the React portal.
/// </summary>
public class BlueprintGenerationResponse
{
    public string RequestId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double Confidence { get; set; }

    /// <summary>Analytics Deployment Contract as a raw JSON element (validated before storage).</summary>
    public JsonElement? AnalyticsDeploymentContract { get; set; }

    /// <summary>Base-64 encoded PDF bytes returned by AgentHost when GeneratePdf=true.</summary>
    public string? BlueprintPdf { get; set; }

    public List<string>? Warnings { get; set; }
    public List<string>? Messages { get; set; }

    /// <summary>Wall-clock execution time reported by AgentHost in milliseconds.</summary>
    public long ExecutionTime { get; set; }

    // ── Derived helpers ──────────────────────────────────────────────────────────

    public string? AnalyticsDeploymentContractJson =>
        AnalyticsDeploymentContract?.GetRawText();

    public byte[]? BlueprintPdfBytes =>
        string.IsNullOrWhiteSpace(BlueprintPdf) ? null : Convert.FromBase64String(BlueprintPdf);
}
