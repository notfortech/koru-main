using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Services;
using StudioTechBI.Domain.Interfaces;
using StudioTechBI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.DTOs.InsightsEngine;
using StudioTechBI.Application.Models;

namespace StudioTechBI.API.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly ReportWorkerService _worker;
    private readonly ILogger<ReportsController> _logger;
    private readonly IClientService _clientService;
    private readonly IClientByCompanyQuery _clientByCompanyQuery;
    private readonly IPowerBiAssetQuery _powerBiAssetQuery;
    private readonly IInsightReportService _insightReportService;
    private readonly IModelRepository _insightModelRepository;
    private readonly ApplicationDbContext _db;
    private readonly IInsightsEngineReportInsightsClient _reportInsights;
    private readonly IOptionsMonitor<InsightsEngineOptions> _insightsEngineOptions;
    private readonly IPowerBiDatasetSampleService _datasetSamples;

    public ReportsController(
        ReportWorkerService worker,
        ILogger<ReportsController> logger,
        IClientService clientService,
        IClientByCompanyQuery clientByCompanyQuery,
        IPowerBiAssetQuery powerBiAssetQuery,
        IInsightReportService insightReportService,
        IModelRepository insightModelRepository,
        ApplicationDbContext db,
        IInsightsEngineReportInsightsClient reportInsights,
        IOptionsMonitor<InsightsEngineOptions> insightsEngineOptions,
        IPowerBiDatasetSampleService datasetSamples)
    {
        _worker = worker;
        _logger = logger;
        _clientService = clientService;
        _clientByCompanyQuery = clientByCompanyQuery;
        _powerBiAssetQuery = powerBiAssetQuery;
        _insightReportService = insightReportService;
        _insightModelRepository = insightModelRepository;
        _db = db;
        _reportInsights = reportInsights;
        _insightsEngineOptions = insightsEngineOptions;
        _datasetSamples = datasetSamples;
    }

    public sealed class CreateReportRequestBody
    {
        public string TemplateId { get; set; } = string.Empty;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? BlobPath { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ClientCode { get; set; }

        public bool UseSelectedClient { get; set; }
    }

    /// <summary>
    /// Creates a pending report request for downstream processing (reporting.ReportRequests).
    /// Intended for Insights UI \"Confirm template\" action.
    /// </summary>
    [HttpPost("requests")]
    public async Task<IActionResult> CreateReportRequest([FromBody] CreateReportRequestBody body, CancellationToken ct)
    {
        if (body == null || string.IsNullOrWhiteSpace(body.TemplateId) || !Guid.TryParse(body.TemplateId.Trim(), out var templateId) || templateId == Guid.Empty)
            return BadRequest(new { success = false, message = "templateId must be a non-empty GUID." });

        // Resolve client similarly to other firm-aware flows.
        var effectiveClientCode = body.UseSelectedClient ? body.ClientCode?.Trim() : User.FindFirstValue("client_code")?.Trim();
        if (string.IsNullOrWhiteSpace(effectiveClientCode))
            return BadRequest(new { success = false, message = "clientCode is required (missing client_code claim)." });

        var accessible = await GetAccessibleClientCodesAsync(ct);
        if (body.UseSelectedClient)
        {
            if (string.IsNullOrWhiteSpace(body.ClientCode) || !accessible.Any(a => string.Equals(a.Code, effectiveClientCode, StringComparison.OrdinalIgnoreCase)))
                return StatusCode(403, new { success = false, message = "You do not have access to this client." });
        }

        var client = await _clientService.GetByClientCodeOrIdAsync(effectiveClientCode, ct);
        if (client == null)
            return NotFound(new { success = false, message = "Client not found." });

        if (!accessible.Any(a => a.Id == client.ClientId))
            return StatusCode(403, new { success = false, message = "You do not have access to this client." });

        var templateExists = await _db.Templates.AsNoTracking().AnyAsync(t => !t.IsDeleted && t.Id == templateId, ct);
        if (!templateExists)
            return NotFound(new { success = false, message = "Template not found." });

        var email = User.FindFirstValue(ClaimTypes.Email)?.Trim();
        var request = new StudioTechBI.Domain.Entities.ReportingReportRequest
        {
            RequestId = Guid.NewGuid(),
            ClientId = client.ClientId,
            TemplateId = templateId,
            SourceBlobPath = string.IsNullOrWhiteSpace(body.BlobPath) ? null : body.BlobPath.Trim(),
            Status = "Pending",
            RequestedByEmail = string.IsNullOrWhiteSpace(email) ? null : email,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.ReportingReportRequests.Add(request);
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            success = true,
            requestId = request.RequestId,
            status = request.Status
        });
    }

    public sealed class ReportAiInsightsBody
    {
        public string? ClientCode { get; set; }
        public bool UseSelectedClient { get; set; }
        public string ReportType { get; set; } = "monthly";
        public string? Period { get; set; }
        public string? ActivePageName { get; set; }
        public List<string>? VisualTitles { get; set; }
    }

    /// <summary>
    /// Returns AI insights for the active embedded Power BI page (metadata-only).
    /// Proxies to InsightsEngine.Api when enabled.
    /// </summary>
    [HttpPost("ai-insights")]
    public async Task<IActionResult> GetAiInsightsForReportPage([FromBody] ReportAiInsightsBody body, CancellationToken ct)
    {
        var opt = _insightsEngineOptions.CurrentValue;
        if (!opt.ExternalCopilotAiEnabled)
        {
            return Ok(new
            {
                enabled = false,
                message = "AI is not enabled for this client."
            });
        }

        var requestedClientCode = (body?.ClientCode ?? "").Trim();
        if (body?.UseSelectedClient == true)
        {
            if (string.IsNullOrWhiteSpace(requestedClientCode))
                return BadRequest(new { enabled = false, message = "clientCode is required when useSelectedClient is true." });
        }

        var effectiveClientCode = body?.UseSelectedClient == true
            ? requestedClientCode
            : User.FindFirstValue("client_code")?.Trim();

        if (string.IsNullOrWhiteSpace(effectiveClientCode))
            return BadRequest(new { enabled = false, message = "clientCode is required (missing client_code claim)." });

        var accessible = await GetAccessibleClientCodesAsync(ct);
        if (body?.UseSelectedClient == true && !accessible.Any(a => string.Equals(a.Code, effectiveClientCode, StringComparison.OrdinalIgnoreCase)))
            return StatusCode(403, new { enabled = false, message = "You do not have access to this client." });

        var client = await _clientService.GetByClientCodeOrIdAsync(effectiveClientCode, ct);
        if (client == null)
            return NotFound(new { enabled = false, message = "Client not found." });
        if (!accessible.Any(a => a.Id == client.ClientId))
            return StatusCode(403, new { enabled = false, message = "You do not have access to this client." });

        var asset = await _powerBiAssetQuery.GetActiveAssetByClientCodeAsync(
            effectiveClientCode,
            string.IsNullOrWhiteSpace(body?.ReportType) ? "monthly" : body!.ReportType.Trim(),
            ct);

        var activePage = (body?.ActivePageName ?? "").Trim();
        if (activePage.Length == 0)
            activePage = "Active page";

        var identityForRls =
            User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue("preferred_username")
            ?? User.FindFirstValue(ClaimTypes.Upn);

        IReadOnlyList<DatasetQueryTableSample> samples = Array.Empty<DatasetQueryTableSample>();
        try
        {
            samples = await _datasetSamples.GetDatasetSamplesAsync(
                asset,
                string.IsNullOrWhiteSpace(body?.ReportType) ? "monthly" : body!.ReportType.Trim(),
                activePage,
                identityForRls,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dataset sampling for report insights failed; continuing metadata-only for {Client}.", effectiveClientCode);
        }

        var req = new ReportPageInsightsRequest
        {
            ReportType = string.IsNullOrWhiteSpace(body?.ReportType) ? "monthly" : body!.ReportType.Trim(),
            Period = string.IsNullOrWhiteSpace(body?.Period) ? null : body!.Period!.Trim(),
            ActivePageName = activePage,
            VisualTitles = body?.VisualTitles?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Take(50).ToList() ?? new List<string>(),
            Prompts = new List<string>
            {
                "What do these visuals suggest?",
                "What is this report conveying?",
                "Any anomalies, risks, or action items a user should notice?",
            },
            DatasetSamples = samples.Count > 0 ? samples.ToList() : null
        };

        try
        {
            var resp = await _reportInsights.GetInsightsFromMetadataAsync(req, ct);
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
            _logger.LogWarning(ex, "AI report insights proxy failed for client {ClientCode}.", effectiveClientCode);
            return StatusCode(502, new { enabled = false, message = "AI insights service is temporarily unavailable." });
        }
    }

    /// <summary>Returns client codes and ids the current user can access (single client from claim + all from companies for accountant).</summary>
    private async Task<IReadOnlyList<(string Code, Guid Id)>> GetAccessibleClientCodesAsync(CancellationToken ct)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email)) return Array.Empty<(string, Guid)>();

        var fromCompanies = await _clientByCompanyQuery.GetClientsForUserEmailAsync(email, ct);
        var list = fromCompanies.Select(c => (Code: c.ClientCode ?? "", c.ClientId)).Where(x => !string.IsNullOrEmpty(x.Code)).ToList();

        var claimCode = User.FindFirstValue("client_code");
        if (!string.IsNullOrEmpty(claimCode) && list.All(x => !string.Equals(x.Code, claimCode, StringComparison.OrdinalIgnoreCase)))
        {
            var fromClaim = await _clientService.GetByClientCodeOrIdAsync(claimCode, ct);
            if (fromClaim != null)
                list.Add((fromClaim.ClientCode ?? fromClaim.BlobFolderPath ?? claimCode, fromClaim.ClientId));
        }
        return list;
    }

    /// <summary>Ensures the route clientId is one of the authenticated user's accessible clients (single client or accountant's list). Returns 403 if not.</summary>
    private async Task<IActionResult?> EnsureClientAccessAsync(string clientId, CancellationToken ct)
    {
        var accessible = await GetAccessibleClientCodesAsync(ct);
        if (accessible.Count == 0)
        {
            _logger.LogWarning("Reports 403: User has no accessible clients.");
            return StatusCode(403, new { success = false, message = "Your account is not linked to a client. Please contact support to configure client access." });
        }
        var normalized = clientId?.Trim() ?? "";
        var hasAccess = accessible.Any(a => string.Equals(a.Code, normalized, StringComparison.OrdinalIgnoreCase) || a.Id.ToString() == normalized);
        if (!hasAccess)
        {
            _logger.LogWarning("Reports 403: User does not have access to client '{RouteClient}'.", clientId);
            return StatusCode(403, new { success = false, message = "You do not have access to this client's reports." });
        }
        return null;
    }

    private async Task<IActionResult?> EnsureInsightModelAccessAsync(Guid modelId, CancellationToken ct)
    {
        var model = await _insightModelRepository.GetByIdAsync(modelId, ct);
        if (model == null)
            return NotFound(new { success = false, message = "Model not found." });

        var accessible = await GetAccessibleClientCodesAsync(ct);
        if (accessible.Count == 0)
            return StatusCode(403, new { success = false, message = "Your account is not linked to a client." });

        if (!accessible.Any(a => a.Id == model.ClientId))
        {
            _logger.LogWarning("Reports 403: User cannot access insight model {ModelId}.", modelId);
            return StatusCode(403, new { success = false, message = "You do not have access to this model." });
        }

        return null;
    }

    /// <summary>Power BI embed token for the report created for an insight model (after orchestration).</summary>
    [HttpGet("{modelId:guid}")]
    public async Task<IActionResult> GetInsightReportEmbed(Guid modelId, CancellationToken cancellationToken)
    {
        var access = await EnsureInsightModelAccessAsync(modelId, cancellationToken);
        if (access != null)
            return access;

        try
        {
            var dto = await _insightReportService.GetInsightReportEmbedAsync(modelId, cancellationToken);
            if (dto == null)
                return NotFound(new { success = false, message = "Model not found." });

            return Ok(new
            {
                datasetId = dto.DatasetId,
                reportId = dto.ReportId,
                embedUrl = dto.EmbedUrl,
                accessToken = dto.AccessToken
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Insight report embed not available for model {ModelId}.", modelId);
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>List of clients the current user can access (for accounting firm: multiple; for single-client user: one). Use clientCode when calling embed-token or reports/available/{clientCode}.</summary>
    [HttpGet("accountant-clients")]
    public async Task<IActionResult> GetAccountantClients(CancellationToken cancellationToken)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email))
            return Ok(Array.Empty<object>());

        var fromCompanies = await _clientByCompanyQuery.GetClientsForUserEmailAsync(email, cancellationToken);
        var list = fromCompanies.Select(c => new { clientId = c.ClientId, clientCode = c.ClientCode ?? "", clientName = c.ClientName ?? "" }).ToList();

        if (list.Count == 0)
        {
            var claimCode = User.FindFirstValue("client_code");
            if (!string.IsNullOrEmpty(claimCode))
            {
                var single = await _clientService.GetByClientCodeOrIdAsync(claimCode, cancellationToken);
                if (single != null)
                    list.Add(new { clientId = single.ClientId, clientCode = single.ClientCode ?? single.BlobFolderPath ?? claimCode, clientName = single.ClientName ?? "" });
            }
        }
        return Ok(list);
    }

    [HttpPost("generate/{clientId}")]
    public async Task<IActionResult> Generate(string clientId, [FromBody] GenerateRequest? request)
    {
        var access = await EnsureClientAccessAsync(clientId, HttpContext.RequestAborted);
        if (access != null) return access;
        if (request == null)
            return BadRequest(new { success = false, message = "Request body is required (PeriodType, Period)." });
        try
        {
            await _worker.Generate(clientId, request.PeriodType ?? "", request.Period ?? "");
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reports generation failed for client {ClientId}", clientId);
            return StatusCode(500, new { success = false, message = "Failed to generate report." });
        }
    }

    /// <summary>Returns the current user's client (folder name, clientId, blob path) and available report periods. Use clientCode (e.g. AU-001) for embed-token and blob/dataset in that folder.</summary>
    [HttpGet("available")]
    public async Task<IActionResult> GetAvailableReports()
    {
        var userClientCode = User.FindFirstValue("client_code");
        if (string.IsNullOrEmpty(userClientCode))
            return Ok(new { clientCode = (string?)null, clientId = (Guid?)null, clientName = (string?)null, blobFolderPath = (string?)null, availablePeriods = Array.Empty<object>() });

        var client = await _clientService.GetByClientCodeOrIdAsync(userClientCode, HttpContext.RequestAborted);
        if (client == null)
            return Ok(new { clientCode = userClientCode, clientId = (Guid?)null, clientName = (string?)null, blobFolderPath = (string?)null, availablePeriods = Array.Empty<object>() });

        var availablePeriods = new[]
        {
            new { label = "February 2026", value = "2026-02" },
            new { label = "March 2026", value = "2026-03" },
            new { label = "April 2026", value = "2026-04" }
        };

        // UI currently shows "monthly" periods only.
        var reportType = "monthly";
        var asset = await _powerBiAssetQuery.GetActiveAssetByClientCodeAsync(userClientCode, reportType, HttpContext.RequestAborted);

        var folderName = client.ClientCode ?? client.BlobFolderPath;
        return Ok(new
        {
            clientCode = folderName,
            clientId = client.ClientId,
            clientName = client.ClientName,
            blobFolderPath = client.BlobFolderPath ?? client.ClientCode ?? folderName,
            powerBIReportId = asset?.ReportId,
            powerBIDatasetId = asset?.DatasetId,
            availablePeriods
        });
    }

    /// <summary>
    /// Returns a list of all active reports (Power BI assets) for the current user's client.
    /// Each item contains reportType, reportId, datasetId, and workspaceId.
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> GetReportsList(CancellationToken cancellationToken)
    {
        var userClientCode = User.FindFirstValue("client_code");
        if (string.IsNullOrEmpty(userClientCode))
            return Ok(new { clientCode = (string?)null, clientId = (Guid?)null, clientName = (string?)null, reports = Array.Empty<object>() });

        var client = await _clientService.GetByClientCodeOrIdAsync(userClientCode, cancellationToken);
        if (client == null)
            return Ok(new { clientCode = userClientCode, clientId = (Guid?)null, clientName = (string?)null, reports = Array.Empty<object>() });

        var folderCode = client.ClientCode ?? client.BlobFolderPath ?? userClientCode;
        var assets = await _powerBiAssetQuery.GetAllActiveAssetsForClientAsync(folderCode, cancellationToken);

        var reports = assets.Select(a => new
        {
            reportType = a.ReportType,
            reportId = a.ReportId,
            datasetId = a.DatasetId,
            workspaceId = a.WorkspaceId,
            createdAt = a.CreatedAt
        }).ToList();

        return Ok(new
        {
            clientCode = folderCode,
            clientId = client.ClientId,
            clientName = client.ClientName,
            reports
        });
    }

    /// <summary>
    /// Returns a list of all active reports (Power BI assets) for a specific client.
    /// For accountant users: allowed if the client is in their accessible list.
    /// For single-client users: allowed only if it matches their own client.
    /// </summary>
    [HttpGet("list/{clientCodeOrId}")]
    public async Task<IActionResult> GetReportsListForClient(
        string clientCodeOrId,
        [FromQuery] bool useSelectedClient = false,
        CancellationToken cancellationToken = default)
    {
        var effective = useSelectedClient
            ? clientCodeOrId?.Trim()
            : User.FindFirstValue("client_code")?.Trim();

        if (string.IsNullOrWhiteSpace(effective))
            return NotFound();

        var accessible = await GetAccessibleClientCodesAsync(cancellationToken);
        if (accessible.Count == 0)
            return NotFound();

        var client = await _clientService.GetByClientCodeOrIdAsync(effective, cancellationToken);
        if (client == null)
            return NotFound();

        var hasAccess = accessible.Any(a =>
            string.Equals(a.Code, client.ClientCode ?? client.BlobFolderPath, StringComparison.OrdinalIgnoreCase)
            || a.Id == client.ClientId);
        if (!hasAccess)
            return StatusCode(403, new { success = false, message = "You do not have access to this client's reports." });

        var folderCode = client.ClientCode ?? client.BlobFolderPath;
        var assets = !string.IsNullOrWhiteSpace(folderCode)
            ? await _powerBiAssetQuery.GetAllActiveAssetsForClientAsync(folderCode!, cancellationToken)
            : Array.Empty<StudioTechBI.Application.DTOs.PowerBI.PowerBiAssetDto>();

        var reports = assets.Select(a => new
        {
            reportType = a.ReportType,
            reportId = a.ReportId,
            datasetId = a.DatasetId,
            workspaceId = a.WorkspaceId,
            createdAt = a.CreatedAt
        }).ToList();

        return Ok(new
        {
            clientCode = folderCode,
            clientId = client.ClientId,
            clientName = client.ClientName,
            reports
        });
    }

    /// <summary>Same as GET available but for a specific client. Use folder name (AU-001) or client Id. For accountant: allowed if client is in their list. For single-client user: allowed if it matches their client.</summary>
    [HttpGet("available/{clientCodeOrId}")]
    public async Task<IActionResult> GetAvailableReportsForClient(
        string clientCodeOrId,
        [FromQuery] bool useSelectedClient = false)
    {
        var effective = useSelectedClient
            ? clientCodeOrId?.Trim()
            : User.FindFirstValue("client_code")?.Trim();

        if (string.IsNullOrWhiteSpace(effective))
            return NotFound();

        var accessible = await GetAccessibleClientCodesAsync(HttpContext.RequestAborted);
        if (accessible.Count == 0)
            return NotFound();

        var client = await _clientService.GetByClientCodeOrIdAsync(effective, HttpContext.RequestAborted);
        if (client == null)
            return NotFound();
        var hasAccess = accessible.Any(a => string.Equals(a.Code, client.ClientCode ?? client.BlobFolderPath, StringComparison.OrdinalIgnoreCase) || a.Id == client.ClientId);
        if (!hasAccess)
            return NotFound();

        var availablePeriods = new[]
        {
            new { label = "February 2026", value = "2026-02" },
            new { label = "March 2026", value = "2026-03" },
            new { label = "April 2026", value = "2026-04" }
        };

        var reportType = "monthly";
        var folderCode = client.ClientCode ?? client.BlobFolderPath;
        var asset = !string.IsNullOrWhiteSpace(folderCode)
            ? await _powerBiAssetQuery.GetActiveAssetByClientCodeAsync(folderCode!, reportType, HttpContext.RequestAborted)
            : null;

        var folderName = client.ClientCode ?? client.BlobFolderPath;
        return Ok(new
        {
            clientCode = folderName,
            clientId = client.ClientId,
            clientName = client.ClientName,
            blobFolderPath = client.BlobFolderPath ?? client.ClientCode ?? folderName,
            powerBIReportId = asset?.ReportId,
            powerBIDatasetId = asset?.DatasetId,
            availablePeriods
        });
    }

    /// <summary>
    /// Report refresh: If the master file has data (optionally for the given period), refreshes the Power BI dataset.
    /// Master xlsx path is static for now. When datasetRefreshed is true, the client portal should refresh the report view.
    /// </summary>
    [HttpPost("refresh/{clientId}")]
    public async Task<IActionResult> RefreshReport(string clientId, [FromQuery] string? period, [FromQuery] string? reportType)
    {
        var access = await EnsureClientAccessAsync(clientId ?? "", HttpContext.RequestAborted);
        if (access != null) return access;
        try
        {
            var (success, message, log, datasetRefreshed) = await _worker.RefreshReportIfUpdated(clientId ?? "", period, reportType ?? "monthly");
            if (!success)
            {
                _logger.LogError(
                    "Report refresh failed for client {ClientId}. WorkerMessage={WorkerMessage}",
                    clientId,
                    message);
                return StatusCode(500, new { success = false, message = "Report refresh failed.", datasetRefreshed = false, reportShouldRefresh = false });
            }
            return Ok(new { success = true, message, error = (string?)null, log, datasetRefreshed, reportShouldRefresh = datasetRefreshed });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception during report refresh for client {ClientId}", clientId);
            return StatusCode(500, new { success = false, message = "Report refresh failed.", datasetRefreshed = false, reportShouldRefresh = false });
        }
    }

    /// <summary>
    /// Process new uploads (validate, append to master, then refresh dataset). Logs to reporting.ProcessingJobs.
    /// </summary>
    [HttpPost("process/{clientId}")]
    public async Task<IActionResult> ProcessUploads(string clientId)
    {
        var access = await EnsureClientAccessAsync(clientId, HttpContext.RequestAborted);
        if (access != null) return access;
        try
        {
            var (success, message, log) = await _worker.ProcessUploadsAsync(clientId);
            if (!success)
            {
                _logger.LogError(
                    "Upload processing failed for client {ClientId}. WorkerMessage={WorkerMessage}",
                    clientId,
                    message);
                return StatusCode(500, new { success = false, message = "File processing failed." });
            }
            return Ok(new { success, message, log });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception during upload processing for client {ClientId}", clientId);
            return StatusCode(500, new { success = false, message = "File processing failed." });
        }
    }
}

public class GenerateRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(16)]
    public string PeriodType { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(16)]
    public string Period { get; set; } = string.Empty;
}
