using Microsoft.AspNetCore.Mvc;
using StudioTechBI.API.Filters;
using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.API.Controllers.Internal;

/// <summary>
/// Machine-to-machine ingestion for AI-boundary events raised by stbi_transformers itself
/// (its own calls to Anthropic/OpenAI) — see AiBoundaryAuditEvent for why this exists.
/// Deliberately not under /api/admin: the caller is stbi_transformers, not an admin user, so
/// this is gated by ServiceApiKeyAuth (a shared secret) instead of the admin JWT policy.
/// </summary>
[ApiController]
[Route("api/internal/ai-boundary-audit-log")]
[ServiceApiKeyAuth]
public class InternalAiBoundaryAuditController : ControllerBase
{
    private readonly IAiBoundaryAuditService _auditService;

    public InternalAiBoundaryAuditController(IAiBoundaryAuditService auditService)
    {
        _auditService = auditService;
    }

    [HttpPost]
    public async Task<IActionResult> Ingest(
        [FromBody] AiBoundaryAuditEventIngestRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CorrelationId) ||
            string.IsNullOrWhiteSpace(request.Service) ||
            string.IsNullOrWhiteSpace(request.Operation))
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("CorrelationId, Service, and Operation are required."));
        }

        var metadataJson = request.MetadataJson ?? "{}";

        switch (request.Phase)
        {
            case "Sent":
                await _auditService.LogSentAsync(
                    request.Service, request.Operation, request.CorrelationId, metadataJson,
                    request.ClientId, request.TargetService, cancellationToken);
                break;

            case "Received":
                await _auditService.LogReceivedAsync(
                    request.Service, request.Operation, request.CorrelationId, metadataJson,
                    request.DurationMs ?? 0, request.StatusCode, request.ClientId, request.TargetService,
                    cancellationToken);
                break;

            case "Failed":
                await _auditService.LogFailedAsync(
                    request.Service, request.Operation, request.CorrelationId, request.DurationMs ?? 0,
                    request.ErrorSummary ?? "(no error summary provided)", request.StatusCode, request.ClientId,
                    request.TargetService, cancellationToken);
                break;

            default:
                return BadRequest(ApiResponse<object>.ErrorResponse($"Unknown Phase '{request.Phase}'. Expected Sent, Received, or Failed."));
        }

        return Ok(ApiResponse<object>.SuccessResponse(new { }, "Recorded."));
    }
}
