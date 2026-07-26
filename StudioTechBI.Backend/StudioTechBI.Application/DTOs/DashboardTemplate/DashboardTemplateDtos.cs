using StudioTechBI.Application.DTOs.ReportDesigner;

namespace StudioTechBI.Application.DTOs.DashboardTemplate;

// ── Dashboard Template Generator (Phase 1+2) ─────────────────────────────────────────────────
// New, wholly separate flow from Report Designer's publish (ReportDesignerController.PublishAsync):
// given an uploaded file and an already-generated blueprint, blends real values (where the
// client's file has a matching column) with clearly-labeled mock values (where it doesn't),
// and patches the authored TMDL's data source to point at the blended dataset. Stops short of
// deploy/report-visual generation (Phase 3+4, not this pass) — output is a semantic-model-ready
// TMDL + blended dataset + provenance log the client can inspect, fix, and take further.

/// <summary>One row per blueprint column: whether it was sourced from the client's upload
/// ("uploaded") or synthesized ("mocked"), and how many rows were produced for it.</summary>
public record ProvenanceEntryDto(string Table, string Column, string DataType, string Source, int RowCount);

public static class ProvenanceSource
{
    public const string Uploaded = "uploaded";
    public const string Mocked = "mocked";
}

/// <summary>Output of report/visual generation (Phase 3): the assembled report.json + minimal
/// .platform manifest, plus a log of type fallbacks, axis inference, and unresolved
/// measure/field references — folded into the same log the response surfaces.</summary>
public record ReportGenerationResult(string ReportJson, string PlatformManifestJson, List<string> Log);

public record GenerateDashboardTemplateResponse(
    string CorrelationId,
    List<ProvenanceEntryDto> Provenance,
    string BlendedDatasetBlobPath,
    string? BlendedDatasetDownloadUrl,
    List<TmdlFileDto> PatchedTmdlFiles,
    bool TmdlPatched,
    string Summary,
    bool Deployed,
    string? WorkspaceId,
    string? DatasetId,
    string? ReportId,
    List<string> VisualGenerationLog,
    string? DesignBlueprintTemplateId,
    string? DesignBlueprintTier,
    string? DesignBlueprintLabel,
    // "MatchedTemplate" when a real published template was found and cloned; "PendingTemplateBuild"
    // when no qualifying match was found (a build request was filed); "MatchCheckFailed" when the
    // match check or the clone itself failed technically. Never falls through to generating a
    // report from scratch below the match threshold — see DashboardTemplateController.GenerateAsync.
    string Source = "PendingTemplateBuild",
    Guid? MatchedTemplateId = null,
    string? MatchedTemplateName = null,
    double? MatchConfidence = null);

// ── Verify Template Match (cheap pre-check, no TMDL authoring / Power BI deploy) ─────────────
// Lets the client find out — before committing to a full generate — whether the template
// catalog already has something worth cloning. Blend + catalog match only.

/// <summary>One ranked catalog candidate returned by the match check, regardless of whether it
/// cleared the publish-ready + confidence bar.</summary>
public record TemplateCandidateSummaryDto(Guid TemplateId, string TemplateName, double Confidence, bool IsPublishReady);

public record VerifyTemplateMatchResponse(
    string CorrelationId,
    // "Matched": a qualifying (IsPublishReady, confidence >= threshold) template was found.
    // "Pending": blend + match check both ran cleanly, nothing cleared the bar — a build
    // request was filed for the team. "Error": the match check itself failed technically —
    // client is told to contact support, real detail is logged for staff.
    string Status,
    string Message,
    List<ProvenanceEntryDto> Provenance,
    string? BlendedDatasetBlobPath,
    string? BlendedDatasetDownloadUrl,
    Guid? MatchedTemplateId,
    string? MatchedTemplateName,
    double? MatchConfidence,
    List<TemplateCandidateSummaryDto> Candidates);
