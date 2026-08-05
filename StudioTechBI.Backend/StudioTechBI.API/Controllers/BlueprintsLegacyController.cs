using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.Application.DTOs.Blueprints;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.API.Controllers;

/// <summary>
/// Backwards-compat shim so Koru's existing calls to /api/blueprint/* keep working
/// while the canonical API has moved to /api/blueprints/*.
/// </summary>
[ApiController]
[Route("api/blueprint")]
[Authorize]
[Produces("application/json")]
public class BlueprintsLegacyController : BaseApiController
{
    private readonly IAiGateway _gateway;
    private readonly IClientService _clientService;
    private readonly ILocalCreditLedgerService _localCredits;
    private readonly ILogger<BlueprintsLegacyController> _logger;

    public BlueprintsLegacyController(
        IAiGateway gateway,
        IClientService clientService,
        ILocalCreditLedgerService localCredits,
        ILogger<BlueprintsLegacyController> logger)
    {
        _gateway = gateway;
        _clientService = clientService;
        _localCredits = localCredits;
        _logger = logger;
    }

    /// <summary>
    /// Koru posts the old body shape; we map it to the new GenerateBlueprintRequest.
    /// Old body: { businessRequirement, industry, existingSchema, clientCode, useSelectedClient }
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> Generate(
        [FromBody] LegacyGenerateBlueprintRequest body,
        CancellationToken ct)
    {
        if (body == null || string.IsNullOrWhiteSpace(body.BusinessRequirement))
            return BadRequest(new { success = false, message = "businessRequirement is required." });

        var clientCode = body.UseSelectedClient
            ? body.ClientCode?.Trim()
            : User.FindFirstValue("client_code")?.Trim();

        if (string.IsNullOrWhiteSpace(clientCode))
            return BadRequest(new { success = false, message = "clientCode is required." });

        var client = await _clientService.GetByClientCodeAsync(clientCode, ct);
        if (client == null)
            return NotFound(new { success = false, message = $"Client '{clientCode}' not found." });

        var request = new GenerateBlueprintRequest
        {
            TenantId = client.ClientId,
            ClientId = client.ClientId,
            Industry = body.Industry?.Trim() ?? client.Industry ?? "General",
            BusinessCapability = body.BusinessRequirement.Trim(),
            BusinessGoal = body.BusinessRequirement.Trim(),
            BusinessRequirements = body.ExistingSchema
        };

        var createdBy = User.FindFirstValue(ClaimTypes.Email)
                        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? "unknown";

        var job = await _gateway.QueueBlueprintGenerationAsync(request, createdBy, ct);

        _logger.LogInformation(
            "Legacy blueprint generate queued for client {ClientCode}. GenerationId={GenerationId}",
            clientCode, job.GenerationId);

        return StatusCode(StatusCodes.Status202Accepted, new
        {
            success = true,
            requestId = job.BlueprintId,
            generationId = job.GenerationId,
            status = job.Status
        });
    }

    /// <summary>
    /// Returns past blueprints for a client. Koru polls this on the history tab.
    /// Old query: ?clientCode=AU-004  (optionally ?useSelectedClient=true)
    /// </summary>
    [HttpGet("requests")]
    public async Task<IActionResult> GetRequests(
        [FromQuery] string? clientCode,
        [FromQuery] bool useSelectedClient = false,
        CancellationToken ct = default)
    {
        var code = useSelectedClient
            ? clientCode?.Trim()
            : User.FindFirstValue("client_code")?.Trim();

        if (string.IsNullOrWhiteSpace(code))
            return Ok(Array.Empty<object>());

        var client = await _clientService.GetByClientCodeAsync(code, ct);
        if (client == null)
            return Ok(Array.Empty<object>());

        var (items, _) = await _gateway.GetBlueprintsAsync(client.ClientId, page: 1, pageSize: 50, ct);

        return Ok(items.Select(b => new
        {
            requestId = b.Id,
            status = b.Status,
            industry = b.Industry,
            pdfDownloadUrl = b.ActiveVersion?.HasPdf == true
                ? Url.Action("GetPdf", "Blueprints", new { id = b.Id }, Request.Scheme)
                : null,
            createdAtUtc = b.CreatedAt,
            updatedAtUtc = b.UpdatedAt,
            versionCount = b.VersionCount
        }));
    }

    /// <summary>
    /// Returns the tenant's real AI credit balance from the local interim ledger (see
    /// LocalCreditLedgerService) -- the same balance Blueprint generation, report model
    /// generation, and "Ask AI Assistant" all draw from, while AgentHost's own plan-based ledger
    /// stays bypassed. Falls back to a null/unknown balance if the tenant can't be resolved.
    /// </summary>
    [HttpGet("credits")]
    public async Task<IActionResult> GetCredits(
        [FromQuery] string? clientCode,
        [FromQuery] bool useSelectedClient = false,
        CancellationToken ct = default)
    {
        var code = useSelectedClient
            ? clientCode?.Trim()
            : User.FindFirstValue("client_code")?.Trim();

        if (string.IsNullOrWhiteSpace(code))
        {
            return Ok(new
            {
                creditsRemaining = (int?)null,
                isUnlimited = false,
                subscriptionPlan = (string?)null,
                resetDate = (DateTimeOffset?)null,
                message = "No client selected."
            });
        }

        var client = await _clientService.GetByClientCodeAsync(code, ct);
        if (client is null)
        {
            return Ok(new
            {
                creditsRemaining = (int?)null,
                isUnlimited = false,
                subscriptionPlan = (string?)null,
                resetDate = (DateTimeOffset?)null,
                message = $"Client '{code}' not found."
            });
        }

        // Local interim balance (see LocalCreditLedgerService) -- AgentHost's own ledger is
        // currently bypassed (always returns a fixed constant), so this is the real, per-client
        // number today.
        var creditsRemaining = await _localCredits.GetBalanceAsync(client.ClientId, ct);

        return Ok(new
        {
            creditsRemaining = (int?)creditsRemaining,
            isUnlimited = false,
            subscriptionPlan = (string?)null,
            resetDate = (DateTimeOffset?)null,
            message = (string?)null
        });
    }
}

/// <summary>Old request shape sent by Koru's blueprint form.</summary>
public class LegacyGenerateBlueprintRequest
{
    public string? BusinessRequirement { get; set; }
    public string? Industry { get; set; }
    public string? ExistingSchema { get; set; }
    public string? ClientCode { get; set; }
    public bool UseSelectedClient { get; set; }
}
