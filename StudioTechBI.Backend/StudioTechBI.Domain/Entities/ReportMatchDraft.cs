namespace StudioTechBI.Domain.Entities;

/// <summary>
/// One client's schema-match session/report-in-progress: which SchemaModel and Template it
/// matched, the confirmed column mapping, and whether it's been published (which starts data
/// retention). Deliberately separate from the pre-existing <c>ReportingReportRequest</c>
/// (reporting.ReportRequests) — that's a simpler "generate this report" ticket with no column
/// mapping, consent, or draft/publish distinction; this is a different, richer concept that
/// happens to share the word "report."
/// </summary>
public class ReportMatchDraft : BaseEntity
{
    public Guid ClientId { get; set; }

    public Guid? SchemaModelId { get; set; }
    public SchemaModel? SchemaModel { get; set; }

    /// <summary>Chosen dashboard template — nullable until the user picks one among a model's candidates.</summary>
    public Guid? TemplateId { get; set; }

    /// <summary>Ties this draft back to the extracted schema it was matched from.</summary>
    public string SchemaHash { get; set; } = string.Empty;

    /// <summary>"Draft" | "Published".</summary>
    public string Status { get; set; } = "Draft";

    public DateTime? PublishedAt { get; set; }

    /// <summary>Fixed 18 months from PublishedAt, set at publish time. Not user-configurable — audit/compliance requirement.</summary>
    public DateTime? DataRetentionExpiresAt { get; set; }

    public ICollection<ReportMatchColumnMapping> ColumnMappings { get; set; } = new List<ReportMatchColumnMapping>();
}
