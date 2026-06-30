using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.Application.DTOs.Blueprints;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.API.Controllers;

/// <summary>
/// Dashboard Blueprint API.
/// POST /api/blueprints/generate returns 202 Accepted immediately.
/// The React portal polls GET /api/blueprints/generations/{generationId} for status.
/// </summary>
[ApiController]
[Route("api/blueprints")]
[Route("api/blueprint")]
[Authorize]
[Produces("application/json")]
public class BlueprintsController : BaseApiController
{
    private readonly IAiGateway _gateway;
    private readonly ILogger<BlueprintsController> _logger;

    public BlueprintsController(IAiGateway gateway, ILogger<BlueprintsController> logger)
    {
        _gateway = gateway;
        _logger = logger;
    }

    // ── Generate ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Queue a new dashboard blueprint generation request.
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
    /// List all blueprints for a tenant (paginated).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<BlueprintDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, total) = await _gateway.GetBlueprintsAsync(tenantId, page, pageSize, cancellationToken);

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
