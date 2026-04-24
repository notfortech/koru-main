namespace StudioTechBI.Domain.Entities;

/// <summary>AI-generated model candidate for a client (InsightEngine).</summary>
public class InsightModel : BaseEntity
{
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;

    public string? MappingJson { get; set; }
    public string? ExcelSchemaJson { get; set; }

    /// <summary>Template identifier from InsightEngine (opaque string).</summary>
    public string? TemplateId { get; set; }

    /// <summary>External model identifier from AI agent (string), for idempotency.</summary>
    public string? ExternalModelId { get; set; }

    public double Confidence { get; set; }

    /// <summary>Draft, ReadyForSelection, Selected, Processing, Active, Failed, …</summary>
    public string Status { get; set; } = InsightWorkflowStatuses.Draft;

    /// <summary>When the user approved this model (single event).</summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>Tabular Object Model JSON (server-only; never returned to frontend).</summary>
    public string? TomJson { get; set; }

    /// <summary>SHA-256 hash (Base64) of ordered column names for schema idempotency.</summary>
    public string? SchemaHash { get; set; }

    /// <summary>Expected or actual validated blob path after orchestration (e.g. …/validated/{modelId}/data.xlsx).</summary>
    public string? ValidatedBlobPath { get; set; }

    public bool IsFallback { get; set; }

    public ICollection<InsightDataset> Datasets { get; set; } = new List<InsightDataset>();
    public ICollection<InsightJob> InsightJobs { get; set; } = new List<InsightJob>();
}
