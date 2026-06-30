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
    private readonly ILogger<BlueprintsLegacyController> _logger;

    public BlueprintsLegacyController(
        IAiGateway gateway,
        IClientService clientService,
        ILogger<BlueprintsLegacyController> logger)
    {
        _gateway = gateway;
        _clientService = clientService;
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
    /// Credits stub — the new system does not track credits.
    /// Returns a safe response so Koru doesn't crash.
    /// </summary>
    [HttpGet("credits")]
    public IActionResult GetCredits(
        [FromQuery] string? clientCode,
        [FromQuery] bool useSelectedClient = false)
    {
        return Ok(new
        {
            creditsRemaining = (int?)null,
            creditsConsumed = (int?)null,
            subscriptionPlan = (string?)null,
            resetDate = (DateTimeOffset?)null,
            message = "Credits are not tracked in the current plan."
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
