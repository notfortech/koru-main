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
    /// callers don't need to branch on whether a template matched before calling this.</summary>
    Task<GeneratedReportDto> AssembleAsync(GeneratedReportDto report, CancellationToken cancellationToken = default);
}
