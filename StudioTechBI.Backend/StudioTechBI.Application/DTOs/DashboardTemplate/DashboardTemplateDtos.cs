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

public record GenerateDashboardTemplateResponse(
    string CorrelationId,
    List<ProvenanceEntryDto> Provenance,
    string BlendedDatasetBlobPath,
    string? BlendedDatasetDownloadUrl,
    List<TmdlFileDto> PatchedTmdlFiles,
    bool TmdlPatched,
    string Summary);
