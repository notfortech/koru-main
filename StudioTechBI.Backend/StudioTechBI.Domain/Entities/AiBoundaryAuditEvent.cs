namespace StudioTechBI.Domain.Entities;

/// <summary>
/// Durable, queryable record of every moment application code crosses the AI boundary —
/// generalizes the structured-log-only pattern ReportDesignerClient originally used
/// (ReportDesigner.SchemaSentToAI / AIResponseReceived) into a real table, so "did the AI ever
/// see more than structural metadata, and did every call get logged" can be answered by a query
/// against durable storage instead of "whatever ILogger happens to be configured to write to."
/// MetadataJson must only ever hold the same class of structural-only summary already proven
/// safe in those log lines (table/column names and counts, never data values, connection
/// strings, or credentials) — see ReportDesignerClient for the boundary this table records.
/// </summary>
public class AiBoundaryAuditEvent : BaseEntity
{
    /// <summary>Ties this event to the matching koru-main request log lines and, once S5 lands, the corresponding stbi_transformers-side log entry for the same call.</summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>Which internal integration crossed the boundary, e.g. "ReportDesigner".</summary>
    public string Service { get; set; } = string.Empty;

    /// <summary>Which call, e.g. "GenerateReportModel", "MatchSchemaModel".</summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>"Sent" | "Received" | "Failed".</summary>
    public string Phase { get; set; } = string.Empty;

    /// <summary>The AI-backed system being called, e.g. "stbi_transformers".</summary>
    public string TargetService { get; set; } = string.Empty;

    /// <summary>Structural-only JSON summary of what was sent or received — never data values.</summary>
    public string MetadataJson { get; set; } = string.Empty;

    public long? DurationMs { get; set; }
    public int? StatusCode { get; set; }
    public bool? Success { get; set; }

    /// <summary>Short, data-free failure reason — populated only when Phase == "Failed".</summary>
    public string? ErrorSummary { get; set; }

    /// <summary>Best-effort traceability to the client whose schema triggered the call. Null when not available at the call site.</summary>
    public Guid? ClientId { get; set; }
}
