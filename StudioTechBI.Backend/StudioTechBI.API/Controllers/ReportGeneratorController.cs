using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.Constants;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.DTOs.DashboardTemplate;
using StudioTechBI.Application.DTOs.InsightsEngine;
using StudioTechBI.Application.DTOs.ReportGenerator;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Options;
using StudioTechBI.Application.Models;
using StudioTechBI.Application.Utilities;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Infrastructure.Data;

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
    private static readonly string[] AllowedExtensions = { ".xlsx", ".csv" };

    // Below this, an HTML template candidate is never surfaced to the user — protects report
    // accuracy/data integrity for the AI-assisted "Verify Template Match" picker. Mirrors
    // DashboardTemplateController.MatchConfidenceThreshold's existing single-source-of-truth
    // pattern for its own (unrelated, Power BI catalog) threshold.
    private const double HtmlMatchConfidenceThreshold = 0.85;

    // Flat per-action cost for the local interim ledger (see LocalCreditLedgerService) -- matches
    // ReportDesignerController.ModelGenerationCreditCost's "1 credit per AI action, for now" stance.
    private const int AiSummaryCreditCost = 1;
    private const string AiSummaryFeature = "report-ai-summary";

    private readonly IReportGeneratorClient _reportGeneratorClient;
    private readonly IInsightsEngineReportInsightsClient _reportInsights;
    private readonly IOptionsMonitor<InsightsEngineOptions> _insightsEngineOptions;
    private readonly IHtmlReportAssemblyService _htmlAssembly;
    private readonly IDashboardTemplateLogWriter _templateLogWriter;
    private readonly IClientService _clientService;
    private readonly ILocalCreditLedgerService _localCredits;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ReportGeneratorController> _logger;
    private readonly IOptions<UploadLimitsOptions> _uploadLimits;
    private readonly IBlobSasUriProvider _sasUriProvider;
    private readonly IReportGenerationJobQueue _jobQueue;
    private readonly IDataBlendService _blendService;
    private readonly IWorkbookWriter _workbookWriter;
    private readonly IBlobStorageService _blobStorage;

    public ReportGeneratorController(
        IReportGeneratorClient reportGeneratorClient,
        IInsightsEngineReportInsightsClient reportInsights,
        IOptionsMonitor<InsightsEngineOptions> insightsEngineOptions,
        IHtmlReportAssemblyService htmlAssembly,
        IDashboardTemplateLogWriter templateLogWriter,
        IClientService clientService,
        ILocalCreditLedgerService localCredits,
        ApplicationDbContext db,
        ILogger<ReportGeneratorController> logger,
        IOptions<UploadLimitsOptions> uploadLimits,
        IBlobSasUriProvider sasUriProvider,
        IReportGenerationJobQueue jobQueue,
        IDataBlendService blendService,
        IWorkbookWriter workbookWriter,
        IBlobStorageService blobStorage)
    {
        _reportGeneratorClient = reportGeneratorClient;
        _reportInsights = reportInsights;
        _insightsEngineOptions = insightsEngineOptions;
        _htmlAssembly = htmlAssembly;
        _templateLogWriter = templateLogWriter;
        _clientService = clientService;
        _localCredits = localCredits;
        _db = db;
        _logger = logger;
        _uploadLimits = uploadLimits;
        _sasUriProvider = sasUriProvider;
        _jobQueue = jobQueue;
        _blendService = blendService;
        _workbookWriter = workbookWriter;
        _blobStorage = blobStorage;
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

    /// <summary>AI-assisted "closest template" fallback (see project plan). Asks the matcher for
    /// the full ranked, role-gate-passed HTML template candidate list -- the same call
    /// VerifyHtmlMatchAsync already makes, just without the &gt;=0.85 filter -- and, if anything
    /// cleared the structural role gate, blends the client's real upload against that candidate's
    /// declared schema (real values where present, deterministic mock values for the gaps),
    /// uploads both the original upload and the blended proposal to blob for staff review, and
    /// recomputes KPIs/charts from the blended data. Returns null (never throws) when no candidate
    /// exists or anything in this pipeline fails -- the caller falls back to the plain no-match gap
    /// log in that case, exactly today's behavior.</summary>
    private async Task<GeneratedReportDto?> TryBuildClosestTemplateBlendAsync(
        IFormFile file,
        string ext,
        ClientDto client,
        string correlationId,
        string? templateId,
        string? filters,
        CancellationToken cancellationToken)
    {
        try
        {
            List<HtmlTemplateCandidateDto> candidates;
            await using (var matchStream = file.OpenReadStream())
            {
                candidates = await _reportGeneratorClient.MatchHtmlTemplateAsync(
                    matchStream, file.FileName, correlationId, cancellationToken);
            }

            if (candidates.Count == 0)
                return null;

            var closest = candidates[0];
            var manifestJson = await _htmlAssembly.GetManifestJsonAsync(closest.TemplateId, cancellationToken);
            if (manifestJson is null)
                return null;

            var (tableName, columns) = HtmlManifestColumnMapper.ToBlendColumns(manifestJson);
            if (columns.Count == 0)
                return null;

            BlendResult blend;
            await using (var blendStream = file.OpenReadStream())
            {
                blend = await _blendService.BlendFromColumnListAsync(blendStream, tableName, columns, cancellationToken);
            }

            if (blend.Tables.Count == 0)
                return null;

            // The blended workbook is never persisted server-side -- it's partially fabricated
            // data (real + mock), so only its *schema* (declared columns, real-vs-mocked
            // provenance) belongs in our logs/blob storage, not the file itself. The bytes are
            // returned inline in this response (base64) so the browser can save it straight to the
            // client's machine with zero extra network hop -- this also sidesteps the CORS-driven
            // "failed to fetch" a separate blob-SAS round trip would hit fetching cross-origin from
            // Azure Blob Storage.
            await using var xlsxStream = await _workbookWriter.WriteAsync(blend.Tables, cancellationToken);
            using var xlsxBuffer = new MemoryStream();
            xlsxStream.Position = 0;
            await xlsxStream.CopyToAsync(xlsxBuffer, cancellationToken);
            var blendedDatasetBase64 = Convert.ToBase64String(xlsxBuffer.ToArray());

            // The client's real, original upload IS persisted (unchanged) -- this is the actual
            // business data staff need to build a real template against; the blend above is just a
            // preview aid, not a data source worth keeping.
            var originalBlobPath = $"{client.ClientId}/report-generator/{correlationId}/original-upload{ext}";
            await using (var originalStream = file.OpenReadStream())
            {
                await _blobStorage.UploadClientBlobAsync(originalBlobPath, originalStream, file.ContentType, cancellationToken);
            }

            xlsxBuffer.Position = 0;
            var blendedResult = await _reportGeneratorClient.GenerateReportAsync(
                xlsxBuffer, "blended-dataset.xlsx", templateId, filters, htmlTemplateId: null,
                correlationId, cancellationToken);

            await _templateLogWriter.LogClosestTemplateBlendAsync(
                client.ClientId, client.ClientName, correlationId,
                closest.TemplateId, closest.TemplateName, blend.Provenance,
                originalBlobPath, cancellationToken);

            var realCount = blend.Provenance.Count(p => p.Source == ProvenanceSource.Uploaded);
            var blendNote =
                $"Your uploaded data was a partial match for our \"{closest.TemplateName}\" dashboard — " +
                $"{realCount} of {blend.Provenance.Count} required fields were found in your file. We've " +
                "generated a sample dataset with the missing fields filled in (already downloaded) so you " +
                "can see what a matching file should look like. Update your data to match this schema and " +
                "re-upload to regenerate with your real data — until then, here's your closest possible " +
                "report using this proposed schema.";

            // HtmlTemplateId/HtmlTemplateName/HtmlMatchConfidence/HtmlReport/RowData stay null even
            // though blendedResult may itself have cleared the HTML auto-pick floor -- this
            // fallback always renders as the classic KPI/chart view, never the branded HTML
            // chrome, and never enables Save Report (see the project plan for why).
            return blendedResult with
            {
                HtmlTemplateId = null,
                HtmlTemplateName = null,
                HtmlMatchConfidence = null,
                HtmlReport = null,
                RowData = null,
                ClosestTemplateId = closest.TemplateId,
                ClosestTemplateName = closest.TemplateName,
                BlendProvenance = blend.Provenance,
                BlendedDatasetFileBase64 = blendedDatasetBase64,
                BlendNote = blendNote,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ReportGenerator.ClosestTemplateBlendFailed CorrelationId={CorrelationId}", correlationId);
            return null;
        }
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
    /// GET /api/report-generator/upload-limits
    /// The server-configured max upload size, so the frontend can reject an oversized file before
    /// spending upload bandwidth on a doomed request, without hardcoding a value that can drift
    /// from the backend's actual configured limit (see UploadLimitsOptions).
    /// </summary>
    [HttpGet("upload-limits")]
    public IActionResult GetUploadLimits()
    {
        return Ok(ApiResponse<object>.SuccessResponse(
            new
            {
                maxUploadBytes = _uploadLimits.Value.MaxUploadBytes,
                maxAsyncUploadBytes = _uploadLimits.Value.MaxAsyncUploadBytes
            },
            "Upload limits retrieved successfully."));
    }

    // ── Large-file upload: direct-to-blob + durable async processing (Phase 1) ─────────────────
    // Below UploadLimits.MaxUploadBytes, /generate above is unchanged and stays the fast path.
    // At/above it, the frontend uses this trio instead: init mints a write-only SAS URL and a
    // Pending job row; the browser PUTs bytes straight to blob (this app tier never buffers the
    // file); complete verifies the REAL committed size server-side (a write SAS carries no byte
    // cap of its own) and only then enqueues the job; the frontend polls the third endpoint for
    // the result. See ReportGenerationJobBackgroundService for the worker that actually processes it.

    private static readonly TimeSpan UploadWriteSasValidFor = TimeSpan.FromMinutes(30);

    /// <summary>POST /api/report-generator/uploads/init — mints a write-only SAS URL for a
    /// server-chosen (never client-supplied) blob path, and persists the expected job/path before
    /// any bytes move.</summary>
    [HttpPost("uploads/init")]
    public async Task<IActionResult> InitUploadAsync([FromBody] ReportUploadInitRequest body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body.FileName))
            return BadRequest(ApiResponse<object>.ErrorResponse("fileName is required."));

        var ext = Path.GetExtension(body.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(ApiResponse<object>.ErrorResponse(
                $"Unsupported file type '{ext}'. Allowed: {string.Join(", ", AllowedExtensions)}"));

        var client = await ResolveClientAsync(cancellationToken);
        if (client is null)
            return BadRequest(ApiResponse<object>.ErrorResponse("Could not resolve the calling client."));

        var jobId = Guid.NewGuid();
        var safeName = SanitizeFileName(body.FileName);
        var blobPath = $"{client.ClientId}/report-generator/uploads/{jobId:N}/{safeName}";

        var writeUrl = await _sasUriProvider.GetWriteSasUriAsync(blobPath, UploadWriteSasValidFor, cancellationToken);
        if (writeUrl is null)
            return StatusCode(502, ApiResponse<object>.ErrorResponse("Could not prepare a direct upload URL. Please try again."));

        _db.ReportGenerationJobs.Add(new ReportGenerationJob
        {
            Id = jobId,
            ClientId = client.ClientId,
            BlobPath = blobPath,
            FileName = body.FileName,
            Status = ReportGenerationJobStatuses.Pending,
            CorrelationId = jobId.ToString(),
        });
        await _db.SaveChangesAsync(cancellationToken);

        var response = new ReportUploadInitResponse(jobId, writeUrl, blobPath, DateTimeOffset.UtcNow.Add(UploadWriteSasValidFor));
        return Ok(ApiResponse<ReportUploadInitResponse>.SuccessResponse(response, "Upload URL created."));
    }

    /// <summary>POST /api/report-generator/uploads/{jobId}/complete — called once the browser's
    /// direct PUT to blob finishes. Verifies the real committed blob size (the actual enforcement
    /// point for this flow) before enqueueing anything.</summary>
    [HttpPost("uploads/{jobId:guid}/complete")]
    public async Task<IActionResult> CompleteUploadAsync(
        Guid jobId, [FromBody] ReportUploadCompleteRequest body, CancellationToken cancellationToken)
    {
        var client = await ResolveClientAsync(cancellationToken);
        if (client is null)
            return BadRequest(ApiResponse<object>.ErrorResponse("Could not resolve the calling client."));

        var job = await _db.ReportGenerationJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null || job.ClientId != client.ClientId)
            return NotFound(ApiResponse<object>.ErrorResponse("Upload not found."));

        if (job.Status != ReportGenerationJobStatuses.Pending)
            return Conflict(ApiResponse<object>.ErrorResponse($"This upload is already {job.Status}."));

        var actualSize = await _sasUriProvider.GetBlobSizeAsync(job.BlobPath, cancellationToken);
        if (actualSize is null)
            return BadRequest(ApiResponse<object>.ErrorResponse("No file was found at the expected upload location. Please upload again."));

        if (actualSize > _uploadLimits.Value.MaxAsyncUploadBytes)
        {
            await _sasUriProvider.DeleteBlobAsync(job.BlobPath, cancellationToken);
            job.Status = ReportGenerationJobStatuses.Failed;
            job.ErrorMessage = $"Uploaded file ({actualSize} bytes) exceeds the {_uploadLimits.Value.MaxAsyncUploadBytes} byte limit.";
            job.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return BadRequest(ApiResponse<object>.ErrorResponse(
                $"Uploaded file exceeds the {_uploadLimits.Value.MaxAsyncUploadBytes / (1024 * 1024)} MB limit."));
        }

        job.RequestPayloadJson = JsonSerializer.Serialize(body);
        await _db.SaveChangesAsync(cancellationToken);

        await _jobQueue.EnqueueAsync(job.Id, cancellationToken);

        return Accepted(ApiResponse<ReportUploadCompleteResponse>.SuccessResponse(
            new ReportUploadCompleteResponse(job.Id, job.Status), "Upload confirmed — processing has started."));
    }

    /// <summary>GET /api/report-generator/jobs/{jobId} — status poll for a large-file job.
    /// Client-scoped: filters by the caller's own resolved ClientId, same posture as
    /// ReportValidationController's run-status endpoint.</summary>
    [HttpGet("jobs/{jobId:guid}")]
    public async Task<IActionResult> GetJobStatusAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var client = await ResolveClientAsync(cancellationToken);
        if (client is null)
            return BadRequest(ApiResponse<object>.ErrorResponse("Could not resolve the calling client."));

        var job = await _db.ReportGenerationJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null || job.ClientId != client.ClientId)
            return NotFound(ApiResponse<object>.ErrorResponse("Job not found."));

        GeneratedReportDto? result = null;
        if (job.Status == ReportGenerationJobStatuses.Completed && !string.IsNullOrWhiteSpace(job.ResultJson))
        {
            result = JsonSerializer.Deserialize<GeneratedReportDto>(
                job.ResultJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }

        return Ok(ApiResponse<ReportGenerationJobStatusResponse>.SuccessResponse(
            new ReportGenerationJobStatusResponse(job.Id, job.Status, result, job.ErrorMessage),
            "Job status retrieved."));
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = (fileName ?? string.Empty).Trim();
        if (name.Length == 0) return "upload.bin";

        name = name.Replace("\\", "_").Replace("/", "_");

        var sb = new System.Text.StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-' || c == ' ')
                sb.Append(c);
        }

        var cleaned = sb.ToString().Trim().Replace(' ', '_');
        return cleaned.Length == 0 ? "upload.bin" : cleaned;
    }

    /// <summary>
    /// POST /api/report-generator/generate
    /// Accepts an Excel/CSV upload and an optional templateId (defaults to
    /// the engine's best rule-based match). Returns real, computed KPI/chart
    /// values — no AI touches this file at any point in the pipeline.
    /// </summary>
    [HttpPost("generate")]
    [RequestSizeLimit(UploadLimits.MaxUploadBytes)]
    public async Task<IActionResult> GenerateAsync(
        IFormFile file,
        [FromForm] string? templateId,
        [FromForm] string? filters,
        [FromForm] string? htmlTemplateId,
        [FromForm] string? mode,
        [FromForm] bool isRefinement,
        [FromForm] string? themePrimary,
        [FromForm] string? themeDark,
        [FromForm] string? themeLight,
        [FromForm] string? themeBg,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.ErrorResponse("No file uploaded."));

        if (file.Length > _uploadLimits.Value.MaxUploadBytes)
            return BadRequest(ApiResponse<object>.ErrorResponse(
                $"File exceeds the {_uploadLimits.Value.MaxUploadBytes / (1024 * 1024)} MB limit."));

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

            ReportThemeOverride? themeOverride = null;
            if (themePrimary is not null || themeDark is not null || themeLight is not null || themeBg is not null)
                themeOverride = new ReportThemeOverride(themePrimary, themeDark, themeLight, themeBg);

            result = await _htmlAssembly.AssembleAsync(result, themeOverride, cancellationToken);

            ClientDto? client = null;
            if (result.HtmlTemplateId is null)
            {
                client = await ResolveClientAsync(cancellationToken);
                var isAiAssisted = string.Equals(mode, "ai", StringComparison.OrdinalIgnoreCase);

                // AI-assisted only: try to find the closest role-gate-passing HTML template
                // (even below the confidence floor) and blend the client's upload against its
                // declared schema, filling gaps with mock data -- gives the client a usable,
                // as-representative-as-possible fallback report instead of a dead end. Strict
                // mode is untouched: staff still just get the real, raw dataset logged below.
                GeneratedReportDto? blended = isAiAssisted && client is not null
                    ? await TryBuildClosestTemplateBlendAsync(
                        file, ext, client, correlationId, templateId, filters, cancellationToken)
                    : null;

                if (blended is not null)
                {
                    result = blended;
                }
                else
                {
                    // No HTML template matched at all -- log the gap so staff can author one
                    // against this real, observed schema shape (mirrors LogBuildRequestAsync's
                    // existing role for the Power BI catalog). Never blocks the response: the
                    // caller already has a perfectly good report (the existing MUI/recharts
                    // fallback renders it), this is purely a backlog entry.
                    var columnNames = result.Kpis.Select(k => k.Column)
                        .Concat(result.Charts.SelectMany(c => c.Series.Select(s => s.Name)))
                        .Distinct()
                        .ToList();
                    await _templateLogWriter.LogHtmlTemplateGapAsync(
                        client?.ClientId, client?.ClientName ?? "Unknown", correlationId, columnNames,
                        matchPath: isAiAssisted ? "AiAssisted" : "Deterministic", bestConfidence: null,
                        cancellationToken);
                }
            }

            // Only a genuinely new report counts toward Report Stats -- a filter re-apply re-POSTs
            // this same endpoint (see ReportGeneratorPage.tsx's refetchWithFilters), so isRefinement
            // is how the frontend tells us not to double-count. Best-effort: a logging failure here
            // must never fail a report the caller already successfully generated.
            if (!isRefinement)
            {
                try
                {
                    client ??= await ResolveClientAsync(cancellationToken);
                    if (client is not null)
                    {
                        _db.ReportGenerationEvents.Add(new ReportGenerationEvent
                        {
                            Id = Guid.NewGuid(),
                            ClientId = client.ClientId,
                            Mode = string.Equals(mode, "ai", StringComparison.OrdinalIgnoreCase)
                                ? ReportGenerationModes.AiAssisted
                                : ReportGenerationModes.Deterministic,
                            TemplateId = result.TemplateId,
                            TemplateName = result.TemplateName,
                            HtmlTemplateId = result.HtmlTemplateId,
                            HtmlTemplateName = result.HtmlTemplateName,
                        });
                        await _db.SaveChangesAsync(cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to log ReportGenerationEvent for correlation {CorrelationId}.", correlationId);
                }
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

        // Best-effort resolution, same posture as LogHtmlTemplateGapAsync's call sites elsewhere in
        // this controller -- an unresolvable claim degrades to "not charged" rather than blocking a
        // summary the caller is otherwise entitled to.
        var client = await ResolveClientAsync(cancellationToken);
        if (client is not null)
        {
            var creditCheck = await _localCredits.CheckAsync(client.ClientId, AiSummaryCreditCost, cancellationToken);
            if (!creditCheck.Allowed)
            {
                return StatusCode(StatusCodes.Status402PaymentRequired, new
                {
                    enabled = false,
                    message = creditCheck.DenialReason ?? "Insufficient AI credits to generate a summary."
                });
            }
        }

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

            if (client is not null)
                await _localCredits.ConsumeAsync(client.ClientId, AiSummaryCreditCost, AiSummaryFeature, cancellationToken);

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
    [RequestSizeLimit(UploadLimits.MaxUploadBytes)]
    public async Task<IActionResult> VerifyHtmlMatchAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.ErrorResponse("No file uploaded."));

        if (file.Length > _uploadLimits.Value.MaxUploadBytes)
            return BadRequest(ApiResponse<object>.ErrorResponse(
                $"File exceeds the {_uploadLimits.Value.MaxUploadBytes / (1024 * 1024)} MB limit."));

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
