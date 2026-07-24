using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.DTOs.Blueprints;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.DTOs.InsightsEngine;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;

namespace StudioTechBI.API.Controllers;

/// <summary>
/// Dashboard Blueprint API.
/// POST /api/blueprints/generate returns 202 Accepted immediately.
/// The React portal polls GET /api/blueprints/generations/{generationId} for status.
/// </summary>
[ApiController]
[Route("api/blueprints")]
[Authorize]
[Produces("application/json")]
public class BlueprintsController : BaseApiController
{
    private readonly IAiGateway _gateway;
    private readonly IClientService _clientService;
    private readonly IInsightsEngineReportInsightsClient _reportInsights;
    private readonly IOptionsMonitor<InsightsEngineOptions> _insightsEngineOptions;
    private readonly ILogger<BlueprintsController> _logger;

    public BlueprintsController(
        IAiGateway gateway,
        IClientService clientService,
        IInsightsEngineReportInsightsClient reportInsights,
        IOptionsMonitor<InsightsEngineOptions> insightsEngineOptions,
        ILogger<BlueprintsController> logger)
    {
        _gateway = gateway;
        _clientService = clientService;
        _reportInsights = reportInsights;
        _insightsEngineOptions = insightsEngineOptions;
        _logger = logger;
    }

    // ── Generate ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Queue a new dashboard blueprint generation request.
    /// ClientId/TenantId are optional in the body — resolved from the JWT client_code claim when absent.
    /// Returns 202 Accepted with a generationId for status polling.
    /// </summary>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(ApiResponse<BlueprintGenerationJobDto>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Generate(
        [FromBody] GenerateBlueprintRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Resolve ClientId from JWT client_code claim when not supplied in the request body.
        if (request.ClientId is null || request.ClientId == Guid.Empty)
        {
            var clientCode = request.ClientCode?.Trim()
                ?? User.FindFirstValue("client_code")?.Trim();

            if (string.IsNullOrWhiteSpace(clientCode))
                return BadRequest(ApiResponse<object>.ErrorResponse(
                    "clientId or a resolvable client_code claim is required."));

            var client = await _clientService.GetByClientCodeOrIdAsync(clientCode, cancellationToken);
            if (client is null)
                return BadRequest(ApiResponse<object>.ErrorResponse(
                    $"Client '{clientCode}' not found."));

            request.ClientId = client.ClientId;

            // TenantId: use the organisation's tenant if available, else fall back to ClientId as the tenant scope.
            if (request.TenantId is null || request.TenantId == Guid.Empty)
                request.TenantId = client.ClientId;
        }
        else if (request.TenantId is null || request.TenantId == Guid.Empty)
        {
            request.TenantId = request.ClientId;
        }

        var createdBy = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

        var job = await _gateway.QueueBlueprintGenerationAsync(request, createdBy, cancellationToken);

        _logger.LogInformation(
            "Blueprint generation queued by {User}. GenerationId={GenerationId} BlueprintId={BlueprintId}",
            createdBy, job.GenerationId, job.BlueprintId);

        return StatusCode(
            StatusCodes.Status202Accepted,
            ApiResponse<BlueprintGenerationJobDto>.SuccessResponse(job, "Blueprint generation queued."));
    }

    // ── Status polling ────────────────────────────────────────────────────────

    /// <summary>
    /// Poll the status of a generation job.
    /// </summary>
    [HttpGet("generations/{generationId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<BlueprintGenerationJobDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGenerationStatus(
        Guid generationId,
        CancellationToken cancellationToken)
    {
        var job = await _gateway.GetGenerationStatusAsync(generationId, cancellationToken);
        if (job is null)
            return NotFound(ApiResponse<object>.ErrorResponse($"Generation {generationId} not found."));

        return Ok(ApiResponse<BlueprintGenerationJobDto>.SuccessResponse(job));
    }

    // ── List / Detail ─────────────────────────────────────────────────────────

    /// <summary>
    /// List all blueprints for the current user's client (paginated).
    /// tenantId query param is optional — resolved from JWT client_code when absent.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<BlueprintDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var resolvedTenantId = tenantId ?? Guid.Empty;

        if (resolvedTenantId == Guid.Empty)
        {
            var clientCode = User.FindFirstValue("client_code")?.Trim();
            if (!string.IsNullOrWhiteSpace(clientCode))
            {
                var client = await _clientService.GetByClientCodeOrIdAsync(clientCode, cancellationToken);
                if (client is not null)
                    resolvedTenantId = client.ClientId;
            }
        }

        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, total) = await _gateway.GetBlueprintsAsync(resolvedTenantId, page, pageSize, cancellationToken);

        var result = new PaginatedResult<BlueprintDto>
        {
            Items = items.ToList(),
            PageNumber = page,
            PageSize = pageSize,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        };

        return Ok(ApiResponse<PaginatedResult<BlueprintDto>>.SuccessResponse(result));
    }

    /// <summary>
    /// Get a single blueprint by ID (includes active version metadata).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<BlueprintDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var blueprint = await _gateway.GetBlueprintAsync(id, cancellationToken);
        if (blueprint is null)
            return NotFound(ApiResponse<object>.ErrorResponse($"Blueprint {id} not found."));

        return Ok(ApiResponse<BlueprintDto>.SuccessResponse(blueprint));
    }

    // ── Artefact downloads ────────────────────────────────────────────────────

    /// <summary>
    /// Download the active version's Blueprint PDF.
    /// </summary>
    [HttpGet("{id:guid}/pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPdf(Guid id, CancellationToken cancellationToken)
    {
        var stream = await _gateway.GetBlueprintPdfAsync(id, cancellationToken);
        if (stream is null)
            return NotFound(ApiResponse<object>.ErrorResponse($"PDF not found for Blueprint {id}."));

        return File(stream, "application/pdf", $"blueprint-{id}.pdf");
    }

    /// <summary>
    /// Download the active version's Analytics Deployment Contract JSON.
    /// </summary>
    [HttpGet("{id:guid}/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJson(Guid id, CancellationToken cancellationToken)
    {
        var json = await _gateway.GetBlueprintJsonAsync(id, cancellationToken);
        if (json is null)
            return NotFound(ApiResponse<object>.ErrorResponse($"Analytics contract not found for Blueprint {id}."));

        return Content(json, "application/json");
    }

    /// <summary>
    /// POST /api/blueprints/{id}/ai-summary
    /// "Ask AI Assistant" — a plain-language explanation of this blueprint, grounded on its own
    /// already-generated JSON (meta/kpis/measures/pages/self_review/confidence), not a fresh AI
    /// call over raw data. Same InsightsEngine proxy pattern already used by the Report
    /// Generator's ai-summary endpoint and the embedded-report AI Insights panel.
    /// </summary>
    [HttpPost("{id:guid}/ai-summary")]
    public async Task<IActionResult> GetAiSummary(Guid id, CancellationToken cancellationToken)
    {
        var opt = _insightsEngineOptions.CurrentValue;
        if (!opt.ExternalCopilotAiEnabled)
            return Ok(new { enabled = false, message = "AI is not enabled for this client." });

        var json = await _gateway.GetBlueprintJsonAsync(id, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
            return NotFound(ApiResponse<object>.ErrorResponse($"No generated JSON found for blueprint '{id}'."));

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return StatusCode(502, new { enabled = false, message = "Blueprint JSON could not be parsed." });
        }
        using var _ = doc;
        var root = doc.RootElement;

        var samples = new List<DatasetQueryTableSample>();
        foreach (var name in new[] { "meta", "self_review", "confidence" })
        {
            var sample = ObjectToSample(root, name);
            if (sample is not null) samples.Add(sample);
        }
        foreach (var name in new[] { "kpis", "measures", "pages" })
        {
            var sample = ArrayToSample(root, name);
            if (sample is not null) samples.Add(sample);
        }

        var pageTitles = root.TryGetProperty("pages", out var pagesEl) && pagesEl.ValueKind == JsonValueKind.Array
            ? pagesEl.EnumerateArray()
                .Select(p => p.TryGetProperty("name", out var n) ? n.GetString() : null)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Take(50)
                .Select(n => n!)
                .ToList()
            : new List<string>();

        var req = new ReportPageInsightsRequest
        {
            ReportType = "blueprint",
            ActivePageName = "Blueprint",
            VisualTitles = pageTitles,
            Prompts = new List<string>
            {
                "DatasetSamples below contains this blueprint's real, already-generated content (its actual meta/self-review/confidence fields, and every KPI/measure/page it defines) — treat every value in it as ground truth and use it directly.",
                "Explain what this deployment blueprint delivers, citing its actual KPI names, measure names, confidence score, and self-review verdict from DatasetSamples — not a generic description of \"a blueprint with N pages.\"",
                "Summarize the key measures/KPIs (by name) and how the report pages are organized.",
                "Call out anything a reviewer should pay attention to — risks, gaps, or a notably low confidence/self-review score, citing the actual numbers.",
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
            _logger.LogWarning(ex, "AI summary proxy failed for blueprint {BlueprintId}.", id);
            return StatusCode(502, new { enabled = false, message = "AI summary service is temporarily unavailable." });
        }
    }

    /// <summary>Flattens a JSON object's scalar properties into a one-row sample. Nested
    /// objects/arrays are skipped to keep it simple — deliberately generic so it works regardless
    /// of the blueprint schema's exact field names.</summary>
    private static DatasetQueryTableSample? ObjectToSample(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var obj) || obj.ValueKind != JsonValueKind.Object)
            return null;

        var columns = new List<string>();
        var row = new Dictionary<string, string>();
        foreach (var prop in obj.EnumerateObject())
        {
            if (prop.Value.ValueKind is JsonValueKind.Object) continue;
            columns.Add(prop.Name);
            row[prop.Name] = ScalarToString(prop.Value);
        }
        return columns.Count == 0 ? null : new DatasetQueryTableSample { Source = propertyName, Columns = columns, Rows = new List<Dictionary<string, string>> { row } };
    }

    /// <summary>Converts a JSON array of objects into a tabular sample — columns are the union of
    /// whatever property names actually appear, so this works regardless of the blueprint
    /// schema's exact field names.</summary>
    private static DatasetQueryTableSample? ArrayToSample(JsonElement root, string propertyName, int maxRows = 20)
    {
        if (!root.TryGetProperty(propertyName, out var arr) || arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0)
            return null;

        var columns = new List<string>();
        var rows = new List<Dictionary<string, string>>();
        foreach (var item in arr.EnumerateArray().Take(maxRows))
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var row = new Dictionary<string, string>();
            foreach (var prop in item.EnumerateObject())
            {
                if (prop.Value.ValueKind is JsonValueKind.Object) continue;
                if (!columns.Contains(prop.Name)) columns.Add(prop.Name);
                row[prop.Name] = ScalarToString(prop.Value);
            }
            if (row.Count > 0) rows.Add(row);
        }
        return rows.Count == 0 ? null : new DatasetQueryTableSample { Source = propertyName, Columns = columns, Rows = rows };
    }

    private static string ScalarToString(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? "",
        JsonValueKind.Number => value.ToString(),
        JsonValueKind.True or JsonValueKind.False => value.GetBoolean().ToString(),
        JsonValueKind.Array => string.Join(", ", value.EnumerateArray().Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString())),
        _ => ""
    };

    // ── Delete ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Soft-delete a blueprint and all its versions.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var blueprint = await _gateway.GetBlueprintAsync(id, cancellationToken);
        if (blueprint is null)
            return NotFound(ApiResponse<object>.ErrorResponse($"Blueprint {id} not found."));

        await _gateway.DeleteBlueprintAsync(id, cancellationToken);

        _logger.LogInformation("Blueprint {BlueprintId} deleted by {User}.", id,
            User.FindFirstValue(ClaimTypes.NameIdentifier));

        return NoContent();
    }
}
