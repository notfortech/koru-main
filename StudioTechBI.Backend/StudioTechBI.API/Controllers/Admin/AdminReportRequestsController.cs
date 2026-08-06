using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.DTOs.ReportDesigner;
using StudioTechBI.Application.DTOs.ReportRequests;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.API.Controllers.Admin;

/// <summary>
/// Staff-facing queue for "request a custom Power BI report" tickets. Modeled on
/// AdminJobsController's minimal list/detail shape. Fulfillment is deliberately manual — no new
/// Power BI automation lives here; an analyst builds the report themselves via the existing,
/// unmodified clone/rebind tooling, then comes back to POST .../fulfill only to record the
/// resulting PowerBiAsset.
/// </summary>
[ApiController]
[Route("api/admin/report-requests")]
[Authorize(Roles = "Admin,SuperAdmin,OperationsAdmin,SupportAdmin")]
public class AdminReportRequestsController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ApplicationDbContext _db;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<AdminReportRequestsController> _logger;

    public AdminReportRequestsController(
        ApplicationDbContext db, IBlobStorageService blobStorage, ILogger<AdminReportRequestsController> logger)
    {
        _db = db;
        _blobStorage = blobStorage;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllAsync([FromQuery] int? limit, CancellationToken cancellationToken)
    {
        var query = _db.CustomReportRequests.OrderByDescending(r => r.CreatedAt).AsQueryable();
        if (limit is > 0)
            query = query.Take(limit.Value);

        var items = await query
            .Select(r => new CustomReportRequestSummaryDto(
                r.Id, r.Status, r.RequestReason, r.RequestedByEmail, r.SourceFileName, r.CreatedAt,
                r.FulfilledAtUtc, r.ExportedToBlobAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<List<CustomReportRequestSummaryDto>>.SuccessResponse(items));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _db.CustomReportRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity is null)
            return NotFound(ApiResponse<object>.ErrorResponse($"Report request {id} not found."));

        ExtractedSchemaDto? schema = null;
        try
        {
            schema = JsonSerializer.Deserialize<ExtractedSchemaDto>(entity.SchemaSnapshotJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse schema snapshot for report request {RequestId}.", id);
        }

        var dto = new CustomReportRequestDetailDto(
            entity.Id, entity.ClientId, entity.Status, entity.RequestReason, entity.RequestedByEmail,
            entity.Notes, schema, entity.SourceFileName, entity.CreatedAt, entity.FulfilledSavedReportId,
            entity.FulfilledAtUtc, entity.FulfilledByEmail, entity.BlobPath, entity.ExportedToBlobAtUtc);

        return Ok(ApiResponse<CustomReportRequestDetailDto>.SuccessResponse(dto));
    }

    /// <summary>
    /// POST /api/admin/report-requests/{id}/export-to-blob
    /// Writes the request's SchemaSnapshotJson to blob storage on demand — the hand-off point for
    /// feeding a data shape with no matching template into the template-generating agent.
    /// Deliberately on-demand rather than scheduled: staff triggers it per-request once they've
    /// actually decided to act on it, same "manual, no new automation" posture as fulfillment.
    /// Re-exporting (calling this again on an already-exported request) simply overwrites the same
    /// blob path and refreshes the timestamp.
    /// </summary>
    [HttpPost("{id:guid}/export-to-blob")]
    public async Task<IActionResult> ExportToBlobAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _db.CustomReportRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity is null)
            return NotFound(ApiResponse<object>.ErrorResponse($"Report request {id} not found."));

        var blobPath = $"{entity.ClientId}/custom-report-requests/{entity.Id}/schema-snapshot.json";
        try
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(entity.SchemaSnapshotJson));
            await _blobStorage.UploadClientBlobAsync(blobPath, stream, "application/json", cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Blob export failed for report request {RequestId} — blob storage not configured.", id);
            return StatusCode(502, ApiResponse<object>.ErrorResponse("Blob storage is not configured."));
        }

        entity.BlobPath = blobPath;
        entity.ExportedToBlobAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "CustomReportRequest.ExportedToBlob RequestId={RequestId} BlobPath={BlobPath}", id, blobPath);

        var response = new ExportCustomReportRequestToBlobResponse(blobPath, entity.ExportedToBlobAtUtc.Value);
        return Ok(ApiResponse<ExportCustomReportRequestToBlobResponse>.SuccessResponse(response, "Schema exported to blob storage."));
    }

    /// <summary>
    /// POST /api/admin/report-requests/{id}/fulfill
    /// Records the analyst-built PowerBiAsset and creates the linking SavedReport row (source
    /// type PowerBiRequestFulfilled) — this is what makes it surface in the client's Saved
    /// Reports list. Both writes happen together so the request can never end up marked
    /// "Fulfilled" without a corresponding SavedReport for the client to see.
    /// </summary>
    [HttpPost("{id:guid}/fulfill")]
    public async Task<IActionResult> FulfillAsync(
        Guid id, [FromBody] FulfillCustomReportRequestDto body, CancellationToken cancellationToken)
    {
        var entity = await _db.CustomReportRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity is null)
            return NotFound(ApiResponse<object>.ErrorResponse($"Report request {id} not found."));

        var asset = await _db.PowerBiAssets.FirstOrDefaultAsync(a => a.AssetId == body.PowerBiAssetId, cancellationToken);
        if (asset is null)
            return BadRequest(ApiResponse<object>.ErrorResponse("The specified Power BI asset was not found."));

        var savedReportId = Guid.NewGuid();
        _db.SavedReports.Add(new SavedReport
        {
            Id = savedReportId,
            ClientId = entity.ClientId,
            Title = "Custom Power BI Report",
            SourceType = SavedReportSourceTypes.PowerBiRequestFulfilled,
            Status = "Active",
            VersionCount = 0,
            PowerBiAssetId = body.PowerBiAssetId,
        });

        entity.Status = CustomReportRequestStatuses.Fulfilled;
        entity.FulfilledSavedReportId = savedReportId;
        entity.FulfilledAtUtc = DateTime.UtcNow;
        entity.FulfilledByEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "CustomReportRequest.Fulfilled RequestId={RequestId} SavedReportId={SavedReportId} PowerBiAssetId={PowerBiAssetId}",
            id, savedReportId, body.PowerBiAssetId);

        return Ok(ApiResponse<object>.SuccessResponse(new { savedReportId }, "Request marked fulfilled."));
    }
}
