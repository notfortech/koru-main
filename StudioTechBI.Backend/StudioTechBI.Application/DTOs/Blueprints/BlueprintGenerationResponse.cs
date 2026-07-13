using System.Text.Json;

namespace StudioTechBI.Application.DTOs.Blueprints;

/// <summary>
/// Raw response received from STBI-AgentHost POST /api/blueprints/generate.
/// Koru stores the Blueprint JSON artefact and never exposes this directly to the React portal.
/// Field shape matches AgentHost's actual BlueprintGenerationResult contract — it has no
/// BlueprintId (Koru's own Blueprint.Id is used instead) and no nested "diagnostics" object;
/// confidence is a flat 0-100 int on the root object.
/// </summary>
public class BlueprintGenerationResponse
{
    public string? Provider { get; set; }

    public string? Model { get; set; }

    public long ProcessingTimeMs { get; set; }

    /// <summary>Blueprint confidence score 0–100, as returned by AgentHost.</summary>
    public int Confidence { get; set; }

    /// <summary>The generated Analytics Deployment Contract document (structured JSON).</summary>
    public JsonElement? Blueprint { get; set; }

    public List<string>? Warnings { get; set; }

    /// <summary>
    /// Absolute URL to AgentHost's own PDF download endpoint for this generation.
    /// Only present when AgentHost's SaveBlueprints setting is enabled. Koru fetches
    /// the bytes from this URL and re-stores them in its own blob storage.
    /// </summary>
    public string? PdfDownloadUrl { get; set; }

    // ── Derived helpers ──────────────────────────────────────────────────────────

    public string? BlueprintJson => Blueprint?.GetRawText();

    public long ExecutionTimeMs => ProcessingTimeMs;

    /// <summary>Confidence as a 0-1 fraction, matching the scale stored on BlueprintVersion/BlueprintGeneration.</summary>
    public double ConfidenceFraction => Confidence / 100.0;
}
