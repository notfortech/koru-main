using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Services;

namespace StudioTechBI.API.Controllers;

[ApiController]
[Route("api/powerbi")]
[Authorize]
public class PowerBIController : ControllerBase
{
    private readonly PowerBIService _service;
    private readonly IClientByCompanyQuery _clientByCompanyQuery;
    private readonly IClientService _clientService;
    private readonly IReportingTechnicalLogWriter _technicalLogWriter;

    public PowerBIController(PowerBIService service, IClientByCompanyQuery clientByCompanyQuery, IClientService clientService, IReportingTechnicalLogWriter technicalLogWriter)
    {
        _service = service;
        _clientByCompanyQuery = clientByCompanyQuery;
        _clientService = clientService;
        _technicalLogWriter = technicalLogWriter;
    }

    /// <summary>Builds list of client codes the current user can access (for accountant: from companies; plus single client from claim if any).</summary>
    private async Task<IReadOnlyList<string>> GetAccessibleClientCodesAsync(CancellationToken ct)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email)) return Array.Empty<string>();

        var fromCompanies = await _clientByCompanyQuery.GetClientsForUserEmailAsync(email, ct);
        var list = fromCompanies.Select(c => c.ClientCode ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList();

        var claimCode = User.FindFirstValue("client_code")?.Trim();
        if (!string.IsNullOrEmpty(claimCode) && !list.Any(c => string.Equals(c, claimCode, StringComparison.OrdinalIgnoreCase)))
        {
            var fromClaim = await _clientService.GetByClientCodeOrIdAsync(claimCode, ct);
            if (fromClaim != null)
                list.Add(fromClaim.ClientCode ?? fromClaim.BlobFolderPath ?? claimCode);
        }
        return list;
    }

    [HttpGet("embed-token/{periodType}")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> Get(
        string periodType,
        [FromQuery] string? period,
        [FromQuery] string? clientCode,
        [FromQuery] bool useSelectedClient = false)
    {
        // Default: use the authenticated user's client_code claim.
        // Accounting firm mode: frontend must set useSelectedClient=true and provide clientCode from dropdown.
        var clientCodeOrId = User.FindFirst("client_code")?.Value?.Trim();

        if (useSelectedClient && !string.IsNullOrWhiteSpace(clientCode))
        {
            var accessible = await GetAccessibleClientCodesAsync(HttpContext.RequestAborted);
            var normalized = clientCode.Trim();
            if (accessible.Any(c => string.Equals(c, normalized, StringComparison.OrdinalIgnoreCase)))
                clientCodeOrId = normalized;
            // else ignore invalid clientCode and use claim
        }

        // Fallback for backward compatibility (only when claim is missing).
        if (string.IsNullOrWhiteSpace(clientCodeOrId))
            clientCodeOrId = clientCode?.Trim();

        try
        {
            var result = await _service.GetEmbedToken(periodType, period, clientCodeOrId);
            return Ok(result);
        }
        catch (InvalidOperationException)
        {
            // Expected when blob master or reporting.PowerBiAssets is missing for this client — not a server fault.
            await _technicalLogWriter.LogAsync(
                "PowerBIService.GetEmbedToken",
                "Warning",
                "Power BI configuration missing for requested client or report type.",
                null,
                HttpContext.RequestAborted);
            return StatusCode(
                StatusCodes.Status422UnprocessableEntity,
                new
                {
                    success = false,
                    message = "Power BI report is not configured for the selected client.",
                    error = "PowerBiNotConfigured"
                });
        }
        catch (Exception)
        {
            await _technicalLogWriter.LogAsync("PowerBIService.GetEmbedToken", "Error", "Embed token request failed.", null, HttpContext.RequestAborted);
            throw;
        }
    }

    [HttpGet("reports")]
    [Authorize(Roles = "Admin,SuperAdmin,OperationsAdmin,SupportAdmin")]
    public async Task<IActionResult> GetReports()
    {
        var result =
            await _service.GetReports();

        return Ok(result);
    }

    [HttpGet("ai-insights/{period}")]
    [Authorize(Roles = "Admin,SuperAdmin,OperationsAdmin,SupportAdmin")]
    public async Task<IActionResult> GetAIInsights(
        string period,
        [FromQuery] string? periodType,
        [FromQuery] string? clientCode,
        [FromServices] AllInsightsService insights)
    {
        var result = await insights.GetInsights(
            periodType ?? "monthly",
            period,
            clientCode);

        return Ok(result);
    }
}