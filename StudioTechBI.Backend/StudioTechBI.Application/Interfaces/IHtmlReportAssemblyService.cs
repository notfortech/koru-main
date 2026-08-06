using StudioTechBI.Application.DTOs.ReportGenerator;

namespace StudioTechBI.Application.Interfaces;

/// <summary>
/// Turns a matched HTML template + the already-computed report data into one persistable HTML
/// string. Always reads the master template (never writes to it, never writes a "working copy"
/// anywhere) — the only place this feature ever persists a rendered copy is the explicit "Save
/// Report" action (SavedReportsController), which uploads whatever HtmlReport this service last
/// produced. See the report-templates architecture decisions in the project plan for why.
/// </summary>
public interface IHtmlReportAssemblyService
{
    /// <summary>No-op (returns <paramref name="report"/> unchanged) when HtmlTemplateId is null —
    /// callers don't need to branch on whether a template matched before calling this.
    /// <paramref name="themeOverride"/> is optional and also a no-op whenever null, or when the
    /// resolved template's manifest has no themeSlots mapping.</summary>
    Task<GeneratedReportDto> AssembleAsync(
        GeneratedReportDto report,
        ReportThemeOverride? themeOverride = null,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves just a template's manifest.json (blob-first, seed-fallback -- same
    /// resolution order AssembleAsync uses for chrome.html) without fetching chrome.html at all.
    /// Returns null when the template id resolves via neither path. Used by the AI-assisted
    /// "closest template" blend fallback, which only needs the manifest's declared schema
    /// (requiredColumns/optionalColumns/dataContract), never the rendered chrome.</summary>
    Task<string?> GetManifestJsonAsync(string templateId, CancellationToken cancellationToken = default);
}
