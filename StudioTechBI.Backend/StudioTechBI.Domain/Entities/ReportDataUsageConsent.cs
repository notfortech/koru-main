namespace StudioTechBI.Domain.Entities;

/// <summary>
/// Consent #2 in the AI-Assisted report flow: confirms the report will use this specific data,
/// worded around storage for audit/compliance purposes. Distinct from
/// <see cref="ReportDesignerConsent"/> (Consent #1, "send my schema to AI for matching") —
/// this one is about data usage/retention, not the AI call.
///
/// Deliberately append-only, unlike ReportDesignerConsent's upsert-once-per-schema pattern:
/// a new row is recorded on every Publish action, since each Publish is a fresh act of
/// committing to store that client's data and needs its own audit trail entry.
/// </summary>
public class ReportDataUsageConsent : BaseEntity
{
    public Guid ReportMatchDraftId { get; set; }
    public ReportMatchDraft ReportMatchDraft { get; set; } = null!;

    public Guid ClientId { get; set; }

    public DateTime ApprovedAt { get; set; }

    /// <summary>User principal (email) who confirmed, for audit purposes.</summary>
    public string ApprovedBy { get; set; } = string.Empty;
}
