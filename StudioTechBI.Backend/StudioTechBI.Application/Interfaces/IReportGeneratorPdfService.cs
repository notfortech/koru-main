using StudioTechBI.Application.DTOs.ReportGenerator;

namespace StudioTechBI.Application.Interfaces;

/// <summary>Renders an already-generated Report Generator result as a downloadable PDF.
/// Purely a rendering concern (QuestPDF), implemented in the Infrastructure layer — no AI,
/// no persistence; the caller streams the returned bytes straight back to the browser.</summary>
public interface IReportGeneratorPdfService
{
    byte[] Generate(GeneratedReportDto report, string? aiSummary = null, List<string>? insights = null);
}
