using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.Application.DTOs.Blueprint;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.API.Controllers;

[ApiController]
[Route("api/blueprint")]
[Authorize]
public class BlueprintController : ControllerBase
{
    private readonly IBlueprintService _blueprintService;
    private readonly IClientService _clientService;
    private readonly IClientByCompanyQuery _clientByCompanyQuery;
    private readonly ILogger<BlueprintController> _logger;

    public BlueprintController(
        IBlueprintService blueprintService,
        IClientService clientService,
        IClientByCompanyQuery clientByCompanyQuery,
        ILogger<BlueprintController> logger)
    {
        _blueprintService = blueprintService;
        _clientService = clientService;
        _clientByCompanyQuery = clientByCompanyQuery;
        _logger = logger;
    }

    /// <summary>
    /// Sends the business requirement to STBI AgentHost and returns a secure PDF download URL.
    /// The frontend triggers an auto-download popup when pdfDownloadUrl is received.
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] BlueprintGenerateRequestDto body, CancellationToken ct)
    {
        if (body == null || string.IsNullOrWhiteSpace(body.BusinessRequirement))
            return BadRequest(new { success = false, message = "businessRequirement is required." });

        var clientCode = ResolveClientCode(body.UseSelectedClient, body.ClientCode);
        if (clientCode == null)
            return BadRequest(new { success = false, message = "clientCode is required (missing client_code claim)." });

        var accessError = await EnsureClientAccessAsync(clientCode, ct);
        if (accessError != null) return accessError;

        var email = User.FindFirstValue(ClaimTypes.Email) ?? "";

        BlueprintGenerateResponseDto result;
        try
        {
            result = await _blueprintService.GenerateAsync(
                clientCode,
                body.BusinessRequirement.Trim(),
                body.Industry?.Trim(),
                body.ExistingSchema,
                email,
                ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("unavailable"))
        {
            _logger.LogError(ex, "Blueprint generation service unavailable for client {ClientCode}", clientCode);
            return StatusCode(502, new { success = false, message = "Blueprint generation service is temporarily unavailable. Please try again shortly." });
        }
        catch (HttpRequestException ex) when ((int?)ex.StatusCode == 402)
        {
            return StatusCode(402, new { success = false, message = "Your blueprint credits are exhausted. Please wait for your credits to reset." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during blueprint generation for client {ClientCode}", clientCode);
            return StatusCode(500, new { success = false, message = "An unexpected error occurred." });
        }

        return Ok(new
        {
            success = true,
            requestId = result.RequestId,
            status = result.Status,
            pdfDownloadUrl = result.PdfDownloadUrl,
            creditsConsumed = result.CreditsConsumed,
            creditsRemaining = result.CreditsRemaining,
            subscriptionPlan = result.SubscriptionPlan,
            resetDate = result.ResetDate,
            warnings = result.Warnings
        });
    }

    /// <summary>Returns the client's current credit balance from the most recent blueprint.</summary>
    [HttpGet("credits")]
    public async Task<IActionResult> GetCredits(
        [FromQuery] string? clientCode,
        [FromQuery] bool useSelectedClient = false,
        CancellationToken ct = default)
    {
        var code = ResolveClientCode(useSelectedClient, clientCode);
        if (code == null)
            return Ok(new { creditsRemaining = (int?)null, message = "No client associated with this account." });

        var accessError = await EnsureClientAccessAsync(code, ct);
        if (accessError != null) return accessError;

        var credits = await _blueprintService.GetCreditsAsync(code, ct);
        if (credits == null)
            return Ok(new { creditsRemaining = (int?)null, message = "No blueprints have been generated yet." });

        return Ok(new
        {
            creditsRemaining = credits.CreditsRemaining,
            creditsConsumed = credits.CreditsConsumed,
            subscriptionPlan = credits.SubscriptionPlan,
            resetDate = credits.ResetDate
        });
    }

    /// <summary>Lists past blueprints for the client, newest first.</summary>
    [HttpGet("requests")]
    public async Task<IActionResult> GetRequests(
        [FromQuery] string? clientCode,
        [FromQuery] bool useSelectedClient = false,
        CancellationToken ct = default)
    {
        var code = ResolveClientCode(useSelectedClient, clientCode);
        if (code == null) return Ok(Array.Empty<object>());

        var accessError = await EnsureClientAccessAsync(code, ct);
        if (accessError != null) return accessError;

        var blueprints = await _blueprintService.GetRequestsAsync(code, ct);

        return Ok(blueprints.Select(b => new
        {
            requestId = b.Id,
            status = b.Status,
            businessRequirement = b.BusinessRequirement,
            industry = b.Industry,
            pdfDownloadUrl = b.PdfDownloadUrl,
            creditsConsumed = b.CreditsConsumed,
            creditsRemaining = b.CreditsRemaining,
            subscriptionPlan = b.SubscriptionPlan,
            resetDate = b.ResetDate,
            requestedByEmail = b.RequestedByEmail,
            createdAtUtc = b.CreatedAt,
            completedAtUtc = b.CompletedAtUtc
        }));
    }

    /// <summary>
    /// Proxies the PDF stream from AgentHost to the browser.
    /// The frontend calls this URL to trigger the auto-download.
    /// </summary>
    [HttpGet("{blueprintId:guid}/pdf")]
    public async Task<IActionResult> DownloadPdf(Guid blueprintId, CancellationToken ct)
    {
        var accessible = await GetAccessibleClientCodesAsync(ct);
        if (accessible.Count == 0)
            return StatusCode(403, new { success = false, message = "No client access." });

        foreach (var (code, _) in accessible)
        {
            var stream = await _blueprintService.GetPdfStreamAsync(code, blueprintId, ct);
            if (stream != null)
                return File(stream, "application/pdf", $"blueprint-{blueprintId}.pdf");
        }

        return NotFound(new { success = false, message = "PDF not found or not yet ready." });
    }

    // ── helpers (mirrors ReportsController pattern) ──────────────────────────

    private string? ResolveClientCode(bool useSelectedClient, string? providedCode)
    {
        if (useSelectedClient)
            return string.IsNullOrWhiteSpace(providedCode) ? null : providedCode.Trim();

        return User.FindFirstValue("client_code")?.Trim();
    }

    private async Task<IReadOnlyList<(string Code, Guid Id)>> GetAccessibleClientCodesAsync(CancellationToken ct)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email)) return Array.Empty<(string, Guid)>();

        var fromCompanies = await _clientByCompanyQuery.GetClientsForUserEmailAsync(email, ct);
        var list = fromCompanies
            .Select(c => (Code: c.ClientCode ?? "", c.ClientId))
            .Where(x => !string.IsNullOrEmpty(x.Code))
            .ToList();

        var claimCode = User.FindFirstValue("client_code");
        if (!string.IsNullOrEmpty(claimCode) && list.All(x => !string.Equals(x.Code, claimCode, StringComparison.OrdinalIgnoreCase)))
        {
            var fromClaim = await _clientService.GetByClientCodeOrIdAsync(claimCode, ct);
            if (fromClaim != null)
                list.Add((fromClaim.ClientCode ?? claimCode, fromClaim.ClientId));
        }

        return list;
    }

    private async Task<IActionResult?> EnsureClientAccessAsync(string clientCode, CancellationToken ct)
    {
        var accessible = await GetAccessibleClientCodesAsync(ct);
        if (accessible.Count == 0)
            return StatusCode(403, new { success = false, message = "Your account is not linked to a client." });

        var normalized = clientCode.Trim();
        var hasAccess = accessible.Any(a =>
            string.Equals(a.Code, normalized, StringComparison.OrdinalIgnoreCase)
            || a.Id.ToString() == normalized);

        if (!hasAccess)
            return StatusCode(403, new { success = false, message = "You do not have access to this client." });

        return null;
    }
}
