using System.Text.Json;
using StudioTechBI.Application.DTOs.DashboardTemplate;
using StudioTechBI.Application.DTOs.ReportDesigner;

namespace StudioTechBI.Application.Interfaces;

/// <summary>
/// Generates a Power BI report definition (report.json + .platform manifest) with real visuals
/// from either supported blueprint format — deterministic, no AI/LLM call, pure JSON
/// transformation (no I/O), hence synchronous. See ReportVisualGenerator's own remarks for the
/// significant caveats: this is genuinely greenfield (no prior art in either repo) and the
/// generated report.json's innermost visual-binding shape is a best-effort approximation of
/// Power BI's report layout schema, unverified against a live import in this session.
/// </summary>
public interface IReportVisualGenerator
{
    ReportGenerationResult Generate(
        JsonElement blueprint,
        List<BlendedTable> blendedTables,
        List<TmdlFileDto> authoredTmdlFiles,
        string reportDisplayName);
}
