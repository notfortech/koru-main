using System.Text.Json;

namespace StudioTechBI.Application.DTOs.Blueprints;

/// <summary>
/// Raw response received from STBI-AgentHost POST /api/blueprints/generate.
/// Koru stores the Blueprint JSON artefact and never exposes this directly to the React portal.
/// </summary>
public class BlueprintGenerationResponse
{
    public Guid BlueprintId { get; set; }

    public string? Provider { get; set; }

    public string? Model { get; set; }

    public TimeSpan GenerationDuration { get; set; }

    /// <summary>The generated Analytics Deployment Contract document (structured JSON).</summary>
    public JsonElement? Blueprint { get; set; }

    public List<string>? Warnings { get; set; }

    public GenerationDiagnostics? Diagnostics { get; set; }

    // ── Derived helpers ──────────────────────────────────────────────────────────

    public string? BlueprintJson => Blueprint?.GetRawText();

    public long ExecutionTimeMs => (long)GenerationDuration.TotalMilliseconds;

    public double Confidence => Diagnostics?.SchemaValid == true ? 1.0 : 0.0;
}

public class GenerationDiagnostics
{
    public string? CorrelationId { get; set; }

    public int? Tokens { get; set; }

    public int? Attempts { get; set; }

    public bool FallbackUsed { get; set; }

    public bool SchemaValid { get; set; }

    public long? ProviderLatencyMs { get; set; }

    public string? PromptPackVersion { get; set; }

    public string? Status { get; set; }
}
