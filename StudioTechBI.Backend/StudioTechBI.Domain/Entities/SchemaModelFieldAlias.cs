namespace StudioTechBI.Domain.Entities;

/// <summary>
/// A learned alternate name for a <see cref="SchemaModelField"/>'s canonical FieldName.
/// See ReportMatchService for how this is looked up (alongside the exact-normalized-name
/// check) and how new rows get written from an AI-matched result's field mappings.
/// </summary>
public class SchemaModelFieldAlias : BaseEntity
{
    public Guid SchemaModelFieldId { get; set; }
    public SchemaModelField SchemaModelField { get; set; } = null!;

    /// <summary>Raw, as-observed client column name, e.g. "Customer No".</summary>
    public string AliasName { get; set; } = string.Empty;

    /// <summary>ReportMatchService.NormalizeKey(AliasName) — lowercase, alphanumeric only. This is the actual hot-path lookup key.</summary>
    public string NormalizedAliasName { get; set; } = string.Empty;

    /// <summary>Data type observed on the client column when first surfaced — informational only, not authoritative (client data varies).</summary>
    public string? ObservedDataType { get; set; }

    /// <summary>Confidence from the source that produced this alias — the AI's own reported confidence for AI-sourced rows.</summary>
    public double Confidence { get; set; }

    /// <summary>"AiMatched" | "AiProposedNew" | "ManualReview" — mirrors ReportMatchService's MatchSource constants.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>"PendingReview" | "Approved" | "Rejected". Only Approved rows participate in matching lookups.</summary>
    public string ApprovalStatus { get; set; } = "PendingReview";

    /// <summary>How many independent match calls (any client) have surfaced this exact normalized alias. A trust/frequency signal for reviewers — never reset.</summary>
    public int ObservedCount { get; set; } = 1;

    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }

    /// <summary>Traceability — which client/draft first surfaced this alias. Null for admin-seeded rows.</summary>
    public Guid? FirstSeenClientId { get; set; }
    public Guid? FirstSeenReportMatchDraftId { get; set; }
    public ReportMatchDraft? FirstSeenReportMatchDraft { get; set; }

    public string? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
}
