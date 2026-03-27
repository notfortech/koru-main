using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Services;

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

    public ReportsController(
        ReportWorkerService worker,
        ILogger<ReportsController> logger,
        IClientService clientService,
        IClientByCompanyQuery clientByCompanyQuery,
        IPowerBiAssetQuery powerBiAssetQuery)
    {
        _worker = worker;
        _logger = logger;
        _clientService = clientService;
        _clientByCompanyQuery = clientByCompanyQuery;
        _powerBiAssetQuery = powerBiAssetQuery;
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
