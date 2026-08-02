using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.DTOs.ReportGenerator;
using StudioTechBI.Application.DTOs.ReportValidation;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.API.Controllers;

/// <summary>
/// On-demand "Validate Report" pipeline. Client-scoped throughout — every read filters by the
/// caller's own resolved ClientId (never an admin-wide query like AdminLogsController), since this
/// is a client-facing paid feature, not an admin tool.
/// </summary>
[ApiController]
[Route("api/report-validation")]
[Authorize]
public class ReportValidationController : ControllerBase
{
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB, same cap as report-generator
    private static readonly string[] AllowedExtensions = { ".xlsx", ".csv" };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ApplicationDbContext _db;
    private readonly IClientService _clientService;
    private readonly IReportValidationQueue _queue;
    private readonly IReportValidationScratchStorageService _scratchStorage;
    private readonly ILogger<ReportValidationController> _logger;

    public ReportValidationController(
        ApplicationDbContext db,
        IClientService clientService,
        IReportValidationQueue queue,
        IReportValidationScratchStorageService scratchStorage,
        ILogger<ReportValidationController> logger)
    {
        _db = db;
        _clientService = clientService;
        _queue = queue;
        _scratchStorage = scratchStorage;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/report-validation/run
    /// Same file/templateId/filters shape as report-generator/generate, plus the already-generated
    /// GeneratedReportDto (the exact on-screen snapshot) so Data Sanity validates what the user is
    /// actually looking at. Returns 202 with a runId to poll.
    /// </summary>
    [HttpPost("run")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<IActionResult> Run(
        IFormFile file,
        [FromForm] string? templateId,
        [FromForm] string? filters,
        [FromForm] string report,
        CancellationToken cancellationToken)
    {
        var client = await ResolveClientAsync(cancellationToken);
        if (client is null)
            return BadRequest(ApiResponse<object>.ErrorResponse(
                "A resolvable client_code claim is required."));

        if (!client.HasReportValidationAddOn)
            return StatusCode(StatusCodes.Status402PaymentRequired, ApiResponse<object>.ErrorResponse(
                "Report Validation is not enabled for this client."));

        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.ErrorResponse("No file uploaded."));

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(ApiResponse<object>.ErrorResponse("File exceeds the 50 MB limit."));

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(ApiResponse<object>.ErrorResponse(
                $"Unsupported file type '{ext}'. Allowed: {string.Join(", ", AllowedExtensions)}"));

        if (string.IsNullOrWhiteSpace(report))
            return BadRequest(ApiResponse<object>.ErrorResponse("No report data supplied to validate."));

        GeneratedReportDto? reportDto;
        try
        {
            reportDto = JsonSerializer.Deserialize<GeneratedReportDto>(report, JsonOptions);
        }
        catch (JsonException)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("Report data could not be parsed."));
        }
        if (reportDto is null)
            return BadRequest(ApiResponse<object>.ErrorResponse("Report data could not be parsed."));

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(ApiResponse<object>.ErrorResponse("Could not resolve the requesting user."));

        var runId = Guid.NewGuid();

        string scratchBlobPath;
        try
        {
            await using var stream = file.OpenReadStream();
            scratchBlobPath = await _scratchStorage.StoreAsync(runId, file.FileName, stream, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store scratch file for report validation run {RunId}.", runId);
            return StatusCode(500, ApiResponse<object>.ErrorResponse("Could not accept the uploaded file."));
        }

        var run = new ReportValidationRun
        {
            Id = runId,
            ClientId = client.ClientId,
            RequestedByUserId = userId,
            Status = ReportValidationStatuses.Pending,
            TemplateId = templateId ?? reportDto.TemplateId,
            TemplateName = reportDto.TemplateName,
            FiltersJson = filters,
            ReportSnapshotJson = report,
            SourceFileScratchBlobPath = scratchBlobPath,
        };

        _db.ReportValidationRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        await _queue.EnqueueAsync(run.Id, cancellationToken);

        _logger.LogInformation(
            "ReportValidation.Queued RunId={RunId} ClientId={ClientId} RequestedBy={RequestedBy}",
            run.Id, client.ClientId, userId);

        return StatusCode(
            StatusCodes.Status202Accepted,
            ApiResponse<StartReportValidationResponse>.SuccessResponse(
                new StartReportValidationResponse(run.Id, run.Status), "Report validation queued."));
    }

    /// <summary>
    /// GET /api/report-validation/runs/{id}
    /// Poll endpoint. 404s if the run doesn't belong to the caller's own resolved client.
    /// </summary>
    [HttpGet("runs/{id:guid}")]
    public async Task<IActionResult> GetRun(Guid id, CancellationToken cancellationToken)
    {
        var client = await ResolveClientAsync(cancellationToken);
        if (client is null)
            return BadRequest(ApiResponse<object>.ErrorResponse(
                "A resolvable client_code claim is required."));

        var run = await _db.ReportValidationRuns
            .Include(r => r.Checks)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (run is null || run.ClientId != client.ClientId)
            return NotFound(ApiResponse<object>.ErrorResponse($"Report validation run {id} not found."));

        return Ok(ApiResponse<ReportValidationRunDto>.SuccessResponse(MapToDto(run)));
    }

    /// <summary>
    /// GET /api/report-validation/runs
    /// Paginated history for the caller's own client — no clientId query param, unlike
    /// AdminLogsController, since this is client-scoped by design.
    /// </summary>
    [HttpGet("runs")]
    public async Task<IActionResult> GetRuns(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(cancellationToken);
        if (client is null)
            return BadRequest(ApiResponse<object>.ErrorResponse(
                "A resolvable client_code claim is required."));

        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var query = _db.ReportValidationRuns
            .Where(r => r.ClientId == client.ClientId)
            .OrderByDescending(r => r.CreatedAt);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ReportValidationRunSummaryDto(r.Id, r.Status, r.OverallResult, r.TemplateName, r.CreatedAt, r.CompletedAt))
            .ToListAsync(cancellationToken);

        var result = new PaginatedResult<ReportValidationRunSummaryDto>
        {
            Items = items,
            PageNumber = page,
            PageSize = pageSize,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        };

        return Ok(ApiResponse<PaginatedResult<ReportValidationRunSummaryDto>>.SuccessResponse(result));
    }

    private async Task<ClientDto?> ResolveClientAsync(CancellationToken ct)
    {
        var clientCode = User.FindFirstValue("client_code")?.Trim();
        if (string.IsNullOrWhiteSpace(clientCode))
            return null;

        return await _clientService.GetByClientCodeOrIdAsync(clientCode, ct);
    }

    private static ReportValidationRunDto MapToDto(ReportValidationRun run) => new(
        run.Id,
        run.Status,
        run.OverallResult,
        run.TemplateId,
        run.TemplateName,
        run.Checks
            .OrderBy(c => c.CheckFamily)
            .ThenBy(c => c.SortOrder)
            .Select(c => new ReportValidationCheckDto(
                c.CheckFamily,
                c.CheckName,
                c.Status,
                c.Detail,
                string.IsNullOrWhiteSpace(c.EvidenceJson) ? null : JsonSerializer.Deserialize<object>(c.EvidenceJson, JsonOptions)))
            .ToList(),
        run.CreatedAt,
        run.CompletedAt,
        run.ErrorMessage);
}
