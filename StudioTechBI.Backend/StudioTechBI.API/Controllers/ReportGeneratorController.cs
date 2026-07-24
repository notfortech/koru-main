using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.DTOs.InsightsEngine;
using StudioTechBI.Application.DTOs.ReportGenerator;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;

namespace StudioTechBI.API.Controllers;

/// <summary>
/// Report Generator pipeline: connect to real data (Excel/CSV upload) → a
/// deterministic, no-AI engine matches a standard template and computes
/// real KPI/chart values. Unlike ReportDesignerController, this path
/// intentionally forwards the actual file — the receiving service
/// (DashboardAgents.ReportAgent.Api) has no AI/LLM call anywhere in it.
/// </summary>
[ApiController]
[Route("api/report-generator")]
[Authorize]
public class ReportGeneratorController : ControllerBase
{
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB
    private static readonly string[] AllowedExtensions = { ".xlsx", ".csv" };

    private readonly IReportGeneratorClient _reportGeneratorClient;
    private readonly IInsightsEngineReportInsightsClient _reportInsights;
    private readonly IOptionsMonitor<InsightsEngineOptions> _insightsEngineOptions;
    private readonly IReportGeneratorPdfService _pdfService;
    private readonly ILogger<ReportGeneratorController> _logger;

    public ReportGeneratorController(
        IReportGeneratorClient reportGeneratorClient,
        IInsightsEngineReportInsightsClient reportInsights,
        IOptionsMonitor<InsightsEngineOptions> insightsEngineOptions,
        IReportGeneratorPdfService pdfService,
        ILogger<ReportGeneratorController> logger)
    {
        _reportGeneratorClient = reportGeneratorClient;
        _reportInsights = reportInsights;
        _insightsEngineOptions = insightsEngineOptions;
        _pdfService = pdfService;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/report-generator/templates
    /// Lists the standard-template registry (metadata only — id, name,
    /// industry, required column shape). No data is involved in this call.
    /// </summary>
    [HttpGet("templates")]
    public async Task<IActionResult> ListTemplatesAsync(CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid().ToString();
        try
        {
            var templates = await _reportGeneratorClient.ListTemplatesAsync(correlationId, cancellationToken);
            return Ok(ApiResponse<List<ReportTemplateDto>>.SuccessResponse(templates, "Templates retrieved successfully."));
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(
                (int)(ex.StatusCode ?? System.Net.HttpStatusCode.BadGateway),
                ApiResponse<object>.ErrorResponse(ex.Message));
        }
    }

    /// <summary>
    /// POST /api/report-generator/generate
    /// Accepts an Excel/CSV upload and an optional templateId (defaults to
    /// the engine's best rule-based match). Returns real, computed KPI/chart
    /// values — no AI touches this file at any point in the pipeline.
    /// </summary>
    [HttpPost("generate")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<IActionResult> GenerateAsync(
        IFormFile file,
        [FromForm] string? templateId,
        [FromForm] string? filters,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.ErrorResponse("No file uploaded."));

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(ApiResponse<object>.ErrorResponse("File exceeds the 50 MB limit."));

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(ApiResponse<object>.ErrorResponse(
                $"Unsupported file type '{ext}'. Allowed: {string.Join(", ", AllowedExtensions)}"));

        var correlationId = Guid.NewGuid().ToString();

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _reportGeneratorClient.GenerateReportAsync(
                stream, file.FileName, templateId, filters, correlationId, cancellationToken);

            return Ok(ApiResponse<GeneratedReportDto>.SuccessResponse(result, "Report generated successfully."));
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(
                (int)(ex.StatusCode ?? System.Net.HttpStatusCode.BadGateway),
                ApiResponse<object>.ErrorResponse(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse(ex.Message));
        }
    }

    /// <summary>
    /// POST /api/report-generator/ai-summary
    /// Given an already-generated report result (no re-upload of the source file — the summary
    /// is grounded on the computed results, not raw data), returns a plain-language AI summary.
    /// Proxies to the same InsightsEngine.Api used by the embedded-report AI Insights panel
    /// (see ReportsController.GetAiInsightsForReportPage) — the engine's computed KPIs/charts
    /// are already a "small tabular excerpt for LLM grounding", exactly what
    /// DatasetQueryTableSample is for, so this reuses that client/gate unchanged.
    /// </summary>
    [HttpPost("ai-summary")]
    public async Task<IActionResult> GetAiSummaryAsync([FromBody] GeneratedReportDto report, CancellationToken cancellationToken)
    {
        var opt = _insightsEngineOptions.CurrentValue;
        if (!opt.ExternalCopilotAiEnabled)
        {
            return Ok(new { enabled = false, message = "AI is not enabled for this client." });
        }

        if (report is null || (report.Kpis.Count == 0 && report.Charts.Count == 0))
            return BadRequest(new { enabled = false, message = "No report data supplied to summarize." });

        var samples = new List<DatasetQueryTableSample>();
        if (report.Kpis.Count > 0)
        {
            samples.Add(new DatasetQueryTableSample
            {
                Source = "kpis",
                Columns = new List<string> { "Label", "Value", "Aggregation", "Change", "Unit" },
                Rows = report.Kpis.Take(50).Select(k => new Dictionary<string, string>
                {
                    ["Label"] = k.Label,
                    ["Value"] = k.Value.ToString("G"),
                    ["Aggregation"] = k.Aggregation,
                    ["Change"] = k.Change?.ToString("G") ?? "",
                    ["Unit"] = k.Unit ?? "",
                }).ToList()
            });
        }
        foreach (var chart in report.Charts.Take(10))
        {
            var labels = chart.Categories ?? chart.X ?? new List<string>();
            var rows = new List<Dictionary<string, string>>();
            for (var i = 0; i < labels.Count && i < 50; i++)
            {
                var row = new Dictionary<string, string> { ["Category"] = labels[i] };
                foreach (var s in chart.Series)
                    row[s.Name] = i < s.Values.Count ? s.Values[i].ToString("G") : "";
                rows.Add(row);
            }
            samples.Add(new DatasetQueryTableSample
            {
                Source = $"chart:{chart.Title}",
                Columns = new List<string> { "Category" }.Concat(chart.Series.Select(s => s.Name)).ToList(),
                Rows = rows
            });
        }

        var req = new ReportPageInsightsRequest
        {
            ReportType = "report-generator",
            ActivePageName = string.IsNullOrWhiteSpace(report.TemplateName) ? "Report" : report.TemplateName!,
            VisualTitles = report.Charts.Select(c => c.Title).Take(50).ToList(),
            Prompts = new List<string>
            {
                "DatasetSamples below contains the report's real, already-computed KPI values and chart data (not a live query) — treat every number in it as ground truth and use it directly.",
                "Interpret the data: cite specific figures from DatasetSamples by name (e.g. \"Average of sale_year is 2,008\", \"Median price totals $17.1M\") — do not describe only the report's page/visual structure, and do not say data values are unavailable when DatasetSamples is present.",
                "Call out any notable trends, comparisons, or outliers visible in the actual numbers.",
                "What should someone reviewing this report pay attention to first, based on the real values?",
            },
            DatasetSamples = samples.Count > 0 ? samples : null
        };

        try
        {
            var resp = await _reportInsights.GetInsightsFromMetadataAsync(req, cancellationToken);
            return Ok(new
            {
                enabled = true,
                provider = resp.Provider,
                summary = resp.Summary,
                insights = resp.Insights,
                followUps = resp.FollowUps
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI summary proxy failed for report template {TemplateId}.", report.TemplateId);
            return StatusCode(502, new { enabled = false, message = "AI summary service is temporarily unavailable." });
        }
    }

    public sealed class ReportPdfExportRequest
    {
        public GeneratedReportDto Report { get; set; } = null!;
        public string? AiSummary { get; set; }
        public List<string>? Insights { get; set; }
    }

    /// <summary>
    /// POST /api/report-generator/export-pdf
    /// Renders the already-generated report result as a downloadable PDF (QuestPDF). Optionally
    /// includes an AI summary/insights block if the caller already fetched one via ai-summary —
    /// this endpoint never triggers an AI call itself.
    /// </summary>
    [HttpPost("export-pdf")]
    public IActionResult ExportPdf([FromBody] ReportPdfExportRequest request)
    {
        if (request?.Report is null)
            return BadRequest(ApiResponse<object>.ErrorResponse("No report data supplied to export."));

        var bytes = _pdfService.Generate(request.Report, request.AiSummary, request.Insights);
        var fileName = SanitizeFileName(request.Report.TemplateName ?? "report") + "-report.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '-' : c).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "report" : cleaned;
    }
}
