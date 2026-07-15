namespace StudioTechBI.Domain.Entities;

/// <summary>
/// Auditable record of a client's consent to send Report Designer schema metadata
/// (column names/types only, never row data) to the AI matching pipeline.
/// One record per (ClientId, SchemaHash) — consent is scoped to a specific schema shape,
/// not granted globally for the client.
/// </summary>
public sealed class ReportDesignerConsent : BaseEntity
{
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;

    /// <summary>Identifies the exact schema shape this consent applies to (see SchemaHashHelper).</summary>
    public string SchemaHash { get; set; } = string.Empty;

    public DateTime ApprovedAt { get; set; }

    /// <summary>User principal (email) who granted consent, for audit purposes.</summary>
    public string ApprovedBy { get; set; } = string.Empty;
}
