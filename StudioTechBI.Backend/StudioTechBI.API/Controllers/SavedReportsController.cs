using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.DTOs.SavedReports;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.API.Controllers;

/// <summary>
/// A client's "Saved Reports" list — explicit, user-chosen snapshots only (see
/// HtmlReportAssemblyService remarks: plain report generation never persists anything). Every
/// read is scoped to the caller's own resolved ClientId, mirroring ReportValidationController's
/// convention — this is a client-facing feature, not an admin tool.
/// </summary>
[ApiController]
[Route("api/saved-reports")]
[Authorize]
public class SavedReportsController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ApplicationDbContext _db;
    private readonly IClientService _clientService;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<SavedReportsController> _logger;

    public SavedReportsController(
        ApplicationDbContext db,
        IClientService clientService,
        IBlobStorageService blobStorage,
        ILogger<SavedReportsController> logger)
    {
        _db = db;
        _clientService = clientService;
        _blobStorage = blobStorage;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/saved-reports
    /// Persists the exact HtmlReport string already on screen as a new SavedReport (v1) —
    /// explicit, one-shot; a client re-generating and re-saving the same report/template pair
    /// creates a new entry rather than silently overwriting a prior save.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SaveAsync([FromBody] SaveReportRequest request, CancellationToken cancellationToken)
    {
        var client = await ResolveClientAsync(cancellationToken);
        if (client is null)
            return BadRequest(ApiResponse<object>.ErrorResponse("A resolvable client_code claim is required."));

        if (request is null || string.IsNullOrWhiteSpace(request.HtmlReport))
            return BadRequest(ApiResponse<object>.ErrorResponse("No report HTML supplied to save."));

        var savedReportId = Guid.NewGuid();
        var title = !string.IsNullOrWhiteSpace(request.HtmlTemplateName)
            ? request.HtmlTemplateName!
            : !string.IsNullOrWhiteSpace(request.TemplateName)
                ? request.TemplateName!
                : "Saved Report";

        var blobPath = $"{client.ClientId}/saved-reports/{savedReportId}/v1/report.html";
        try
        {
            using var htmlStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(request.HtmlReport));
            await _blobStorage.UploadClientBlobAsync(blobPath, htmlStream, "text/html", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload saved report blob for {SavedReportId}.", savedReportId);
            return StatusCode(500, ApiResponse<object>.ErrorResponse("Could not save the report."));
        }

        var savedReport = new SavedReport
        {
            Id = savedReportId,
            ClientId = client.ClientId,
            Title = title,
            SourceType = SavedReportSourceTypes.GeneratedHtml,
            Status = "Active",
            VersionCount = 1,
        };
        var version = new SavedReportVersion
        {
            Id = Guid.NewGuid(),
            SavedReportId = savedReportId,
            VersionNumber = 1,
            HtmlBlobPath = blobPath,
            TemplateId = request.TemplateId,
            TemplateName = request.TemplateName,
            HtmlTemplateId = request.HtmlTemplateId,
            HtmlTemplateName = request.HtmlTemplateName,
            SourceFileName = request.SourceFileName,
            AppliedFiltersJson = request.AppliedFilters is { Count: > 0 }
                ? JsonSerializer.Serialize(request.AppliedFilters, JsonOptions)
                : null,
            GeneratedDate = DateTime.UtcNow,
            IsActive = true,
        };

        _db.SavedReports.Add(savedReport);
        _db.SavedReportVersions.Add(version);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "SavedReports.Saved SavedReportId={SavedReportId} ClientId={ClientId}", savedReportId, client.ClientId);

        return Ok(ApiResponse<SaveReportResponse>.SuccessResponse(
            new SaveReportResponse(savedReportId, 1), "Report saved."));
    }

    /// <summary>GET /api/saved-reports — list for the caller's own client.</summary>
    [HttpGet]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken)
    {
        var client = await ResolveClientAsync(cancellationToken);
        if (client is null)
            return BadRequest(ApiResponse<object>.ErrorResponse("A resolvable client_code claim is required."));

        var items = await _db.SavedReports
            .Where(r => r.ClientId == client.ClientId && r.Status != "Archived")
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new SavedReportSummaryDto(
                r.Id, r.Title, r.SourceType, r.Status, r.CreatedAt, r.UpdatedAt, r.PowerBiAssetId))
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<List<SavedReportSummaryDto>>.SuccessResponse(items));
    }

    /// <summary>GET /api/saved-reports/{id} — detail; streams the active version's HTML for
    /// GeneratedHtml reports, or returns the PowerBiAsset pointer for PowerBiRequestFulfilled.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var client = await ResolveClientAsync(cancellationToken);
        if (client is null)
            return BadRequest(ApiResponse<object>.ErrorResponse("A resolvable client_code claim is required."));

        var savedReport = await _db.SavedReports
            .Include(r => r.Versions)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (savedReport is null || savedReport.ClientId != client.ClientId)
            return NotFound(ApiResponse<object>.ErrorResponse($"Saved report {id} not found."));

        string? html = null;
        if (savedReport.SourceType == SavedReportSourceTypes.GeneratedHtml)
        {
            var activeVersion = savedReport.Versions.FirstOrDefault(v => v.IsActive)
                ?? savedReport.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

            if (activeVersion?.HtmlBlobPath is not null)
            {
                await using var blobStream = await _blobStorage.DownloadBlobAsync(activeVersion.HtmlBlobPath, cancellationToken);
                if (blobStream is not null)
                {
                    using var reader = new StreamReader(blobStream, System.Text.Encoding.UTF8);
                    html = await reader.ReadToEndAsync(cancellationToken);
                }
            }
        }

        return Ok(ApiResponse<SavedReportDetailDto>.SuccessResponse(new SavedReportDetailDto(
            savedReport.Id, savedReport.Title, savedReport.SourceType, savedReport.Status,
            html, savedReport.PowerBiAssetId, savedReport.CreatedAt)));
    }

    /// <summary>DELETE /api/saved-reports/{id} — soft-delete (Status = Archived), same convention
    /// as Blueprint's own Active/Archived lifecycle. Never deletes the underlying blob.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> ArchiveAsync(Guid id, CancellationToken cancellationToken)
    {
        var client = await ResolveClientAsync(cancellationToken);
        if (client is null)
            return BadRequest(ApiResponse<object>.ErrorResponse("A resolvable client_code claim is required."));

        var savedReport = await _db.SavedReports.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (savedReport is null || savedReport.ClientId != client.ClientId)
            return NotFound(ApiResponse<object>.ErrorResponse($"Saved report {id} not found."));

        savedReport.Status = "Archived";
        savedReport.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<object>.SuccessResponse(new { }, "Saved report archived."));
    }

    private async Task<ClientDto?> ResolveClientAsync(CancellationToken ct)
    {
        var clientCode = User.FindFirstValue("client_code")?.Trim();
        if (string.IsNullOrWhiteSpace(clientCode))
            return null;

        return await _clientService.GetByClientCodeOrIdAsync(clientCode, ct);
    }
}
