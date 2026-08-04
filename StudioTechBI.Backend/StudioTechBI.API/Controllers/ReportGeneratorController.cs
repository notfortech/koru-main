using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.DTOs.Admin;
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
///
/// Also owns HTML template matching/assembly — the same engine now also picks (or is told) an
/// interactive HTML report template and returns the fully-assembled HtmlReport string alongside
/// the existing Kpis/Charts, per the "HTML is the primary report format" redesign. That output is
/// always the assembled string only, in-memory — this controller never persists a copy of
/// anything (see SavedReportsController for the one place an explicit "Save Report" click does).
/// </summary>
[ApiController]
[Route("api/report-generator")]
[Authorize]
public class ReportGeneratorController : ControllerBase
{
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB
    private static readonly string[] AllowedExtensions = { ".xlsx", ".csv" };

    // Below this, an HTML template candidate is never surfaced to the user — protects report
    // accuracy/data integrity for the AI-assisted "Verify Template Match" picker. Mirrors
    // DashboardTemplateController.MatchConfidenceThreshold's existing single-source-of-truth
    // pattern for its own (unrelated, Power BI catalog) threshold.
    private const double HtmlMatchConfidenceThreshold = 0.85;

    private readonly IReportGeneratorClient _reportGeneratorClient;
    private readonly IInsightsEngineReportInsightsClient _reportInsights;
    private readonly IOptionsMonitor<InsightsEngineOptions> _insightsEngineOptions;
    private readonly IHtmlReportAssemblyService _htmlAssembly;
    private readonly IDashboardTemplateLogWriter _templateLogWriter;
    private readonly IClientService _clientService;
    private readonly ILogger<ReportGeneratorController> _logger;

    public ReportGeneratorController(
        IReportGeneratorClient reportGeneratorClient,
        IInsightsEngineReportInsightsClient reportInsights,
        IOptionsMonitor<InsightsEngineOptions> insightsEngineOptions,
        IHtmlReportAssemblyService htmlAssembly,
        IDashboardTemplateLogWriter templateLogWriter,
        IClientService clientService,
        ILogger<ReportGeneratorController> logger)
    {
        _reportGeneratorClient = reportGeneratorClient;
        _reportInsights = reportInsights;
        _insightsEngineOptions = insightsEngineOptions;
        _htmlAssembly = htmlAssembly;
        _templateLogWriter = templateLogWriter;
        _clientService = clientService;
        _logger = logger;
    }

    /// <summary>Resolves the caller's own client from the client_code claim — same pattern as
    /// ReportValidationController.ResolveClientAsync (this session) and DashboardController.
    /// Best-effort: gap logging degrades to ClientId=null (still logged, just not attributable)
    /// rather than failing the whole request when the claim can't be resolved.</summary>
    private async Task<ClientDto?> ResolveClientAsync(CancellationToken ct)
    {
        var clientCode = User.FindFirstValue("client_code")?.Trim();
        if (string.IsNullOrWhiteSpace(clientCode))
            return null;

        return await _clientService.GetByClientCodeOrIdAsync(clientCode, ct);
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
        [FromForm] string? htmlTemplateId,
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
            GeneratedReportDto result;
            await using (var stream = file.OpenReadStream())
            {
                result = await _reportGeneratorClient.GenerateReportAsync(
                    stream, file.FileName, templateId, filters, htmlTemplateId, correlationId, cancellationToken);
            }

            result = await _htmlAssembly.AssembleAsync(result, cancellationToken);

            if (result.HtmlTemplateId is null)
            {
                // No HTML template matched at all -- log the gap so staff can author one against
                // this real, observed schema shape (mirrors LogBuildRequestAsync's existing role
                // for the Power BI catalog). Never blocks the response: the caller already has a
                // perfectly good report (the existing MUI/recharts fallback renders it), this is
                // purely a backlog entry.
                var client = await ResolveClientAsync(cancellationToken);
                var columnNames = result.Kpis.Select(k => k.Column)
                    .Concat(result.Charts.SelectMany(c => c.Series.Select(s => s.Name)))
                    .Distinct()
                    .ToList();
                await _templateLogWriter.LogHtmlTemplateGapAsync(
                    client?.ClientId, client?.ClientName ?? "Unknown", correlationId, columnNames,
                    matchPath: "Deterministic", bestConfidence: null, cancellationToken);
            }

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
    public async Task<IActionResult> GetAiSummaryAsync(
        [FromBody] GeneratedReportDto report,
        [FromQuery] string? question,
        CancellationToken cancellationToken)
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

        // A follow-up chip click sends the exact question it asked back here — answer that one
        // question specifically (still grounded on the same DatasetSamples) instead of the
        // general "explain the whole report" prompt set, so the drawer can behave like a small
        // running Q&A rather than only ever regenerating the same top-level summary.
        var prompts = string.IsNullOrWhiteSpace(question)
            ? new List<string>
            {
                "DatasetSamples below contains the report's real, already-computed KPI values and chart data (not a live query) — treat every number in it as ground truth and use it directly.",
                "Interpret the data: cite specific figures from DatasetSamples by name (e.g. \"Average of sale_year is 2,008\", \"Median price totals $17.1M\") — do not describe only the report's page/visual structure, and do not say data values are unavailable when DatasetSamples is present.",
                "Call out any notable trends, comparisons, or outliers visible in the actual numbers.",
                "What should someone reviewing this report pay attention to first, based on the real values?",
            }
            : new List<string> { question.Trim() };

        var req = new ReportPageInsightsRequest
        {
            ReportType = "report-generator",
            ActivePageName = string.IsNullOrWhiteSpace(report.TemplateName) ? "Report" : report.TemplateName!,
            VisualTitles = report.Charts.Select(c => c.Title).Take(50).ToList(),
            Prompts = prompts,
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

    /// <summary>
    /// POST /api/report-generator/verify-html-match
    /// AI-assisted "Verify Template Match" flow: profiles the uploaded file and returns the full
    /// ranked list of HTML template candidates, filtered to >=HtmlMatchConfidenceThreshold so a
    /// low-confidence guess is never surfaced for the user to pick — protects report accuracy.
    /// Only needs the file (no blueprint/data model) since HTML matching works off column
    /// profiles alone. Does not generate or assemble a report — that still happens via the normal
    /// generate call once the caller has (optionally) chosen a candidate from this list.
    /// </summary>
    [HttpPost("verify-html-match")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<IActionResult> VerifyHtmlMatchAsync(IFormFile file, CancellationToken cancellationToken)
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
            List<HtmlTemplateCandidateDto> allCandidates;
            await using (var stream = file.OpenReadStream())
            {
                allCandidates = await _reportGeneratorClient.MatchHtmlTemplateAsync(
                    stream, file.FileName, correlationId, cancellationToken);
            }

            var qualifying = allCandidates
                .Where(c => c.Confidence >= HtmlMatchConfidenceThreshold)
                .OrderByDescending(c => c.Confidence)
                .ToList();

            if (qualifying.Count == 0)
            {
                var client = await ResolveClientAsync(cancellationToken);
                var bestConfidence = allCandidates.Count > 0 ? allCandidates.Max(c => c.Confidence) : (double?)null;
                await _templateLogWriter.LogHtmlTemplateGapAsync(
                    client?.ClientId, client?.ClientName ?? "Unknown", correlationId,
                    columnNames: [], matchPath: "AiAssisted", bestConfidence, cancellationToken);
            }

            var response = new VerifyHtmlTemplateMatchResponse(correlationId, qualifying);
            return Ok(ApiResponse<VerifyHtmlTemplateMatchResponse>.SuccessResponse(response, "Template match check complete."));
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
}
