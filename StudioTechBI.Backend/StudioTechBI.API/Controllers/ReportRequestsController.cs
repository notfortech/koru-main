using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.DTOs.ReportRequests;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.API.Controllers;

/// <summary>
/// Client-facing "request a custom Power BI report" ticket — filed when nothing in the self-serve
/// HTML/PBIP template catalogs matches well enough. Fulfillment is mostly manual (see
/// AdminReportRequestsController): an analyst builds the report by hand using the existing,
/// unmodified Power BI tooling, then attaches the result there. This controller only ever creates
/// the ticket + snapshots the client's schema for staff to see what they're building against.
/// </summary>
[ApiController]
[Route("api/report-requests")]
[Authorize]
public class ReportRequestsController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ApplicationDbContext _db;
    private readonly IClientService _clientService;
    private readonly ILogger<ReportRequestsController> _logger;

    public ReportRequestsController(ApplicationDbContext db, IClientService clientService, ILogger<ReportRequestsController> logger)
    {
        _db = db;
        _clientService = clientService;
        _logger = logger;
    }

    /// <summary>POST /api/report-requests</summary>
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateCustomReportRequestDto request, CancellationToken cancellationToken)
    {
        // No client_code claim required -- filing a ticket and capturing the schema is meant to
        // work even for a not-yet-a-client user (e.g. a self-registered, limited-access sign-up);
        // an admin fills in the client association later, once one exists, before fulfilling.
        var client = await ResolveClientAsync(cancellationToken);

        if (request?.Schema is null)
            return BadRequest(ApiResponse<object>.ErrorResponse("No schema supplied for this request."));

        var requestedByEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;

        // Only the two known reasons are trusted -- an unrecognized or omitted value defaults to
        // NoConfidentMatch rather than persisting arbitrary caller-supplied text into this field.
        var reason = request.Reason is CustomReportRequestReasons.GenerationError
            ? CustomReportRequestReasons.GenerationError
            : CustomReportRequestReasons.NoConfidentMatch;

        var entity = new CustomReportRequest
        {
            Id = Guid.NewGuid(),
            ClientId = client?.ClientId,
            Status = CustomReportRequestStatuses.Pending,
            RequestReason = reason,
            RequestedByEmail = requestedByEmail,
            Notes = request.Notes,
            SchemaSnapshotJson = JsonSerializer.Serialize(request.Schema, JsonOptions),
            SourceFileName = request.Schema.FileName,
        };

        _db.CustomReportRequests.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "CustomReportRequest.Created RequestId={RequestId} ClientId={ClientId} Email={Email}",
            entity.Id, client?.ClientId, requestedByEmail);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<CreateCustomReportRequestResponse>.SuccessResponse(
                new CreateCustomReportRequestResponse(entity.Id), "Request filed — our team will be in touch."));
    }

    private async Task<ClientDto?> ResolveClientAsync(CancellationToken ct)
    {
        var clientCode = User.FindFirstValue("client_code")?.Trim();
        if (string.IsNullOrWhiteSpace(clientCode))
            return null;

        return await _clientService.GetByClientCodeOrIdAsync(clientCode, ct);
    }
}
