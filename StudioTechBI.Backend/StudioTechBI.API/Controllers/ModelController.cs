using System.Net.Http;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.DTOs.Insight;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Interfaces;

namespace StudioTechBI.API.Controllers;

[ApiController]
[Route("api/models")]
[Authorize]
public class ModelController : ControllerBase
{
    private readonly IInsightService _insightService;
    private readonly IModelRepository _insightModelRepository;
    private readonly IClientByCompanyQuery _clientByCompanyQuery;
    private readonly IClientService _clientService;
    private readonly ILogger<ModelController> _logger;

    public ModelController(
        IInsightService insightService,
        IModelRepository insightModelRepository,
        IClientByCompanyQuery clientByCompanyQuery,
        IClientService clientService,
        ILogger<ModelController> logger)
    {
        _insightService = insightService;
        _insightModelRepository = insightModelRepository;
        _clientByCompanyQuery = clientByCompanyQuery;
        _clientService = clientService;
        _logger = logger;
    }

    private async Task<bool> CanAccessClientAsync(Guid clientId, CancellationToken ct)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email)) return false;

        var fromCompanies = await _clientByCompanyQuery.GetClientsForUserEmailAsync(email, ct);
        if (fromCompanies.Any(c => c.ClientId == clientId))
            return true;

        var claimCode = User.FindFirstValue("client_code");
        if (string.IsNullOrEmpty(claimCode)) return false;

        var fromClaim = await _clientService.GetByClientCodeOrIdAsync(claimCode, ct);
        return fromClaim != null && fromClaim.ClientId == clientId;
    }

    public sealed class GenerateModelsApiRequest
    {
        /// <summary>
        /// Client identifier. Accepts a GUID (ClientId) or a client code (e.g. AU-004).
        /// </summary>
        public string ClientId { get; set; } = "";
        /// <summary>Optional full blob path; otherwise the latest file under accounting/created/ is used.</summary>
        public string? BlobPath { get; set; }
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateModelsApiRequest request, CancellationToken cancellationToken)
    {
        var raw = (request.ClientId ?? "").Trim();
        if (raw.Length == 0)
            return BadRequest(ApiResponse<object>.ErrorResponse("ClientId is required."));

        var resolved = await _clientService.GetByClientCodeOrIdAsync(raw, cancellationToken);
        if (resolved == null)
            return NotFound(ApiResponse<object>.ErrorResponse("Client not found."));

        var clientGuid = resolved.ClientId;
        if (clientGuid == Guid.Empty)
            return BadRequest(ApiResponse<object>.ErrorResponse("ClientId is invalid."));

        if (!await CanAccessClientAsync(clientGuid, cancellationToken))
            return StatusCode(403, ApiResponse<object>.ErrorResponse("You do not have access to this client."));

        try
        {
            var list = await _insightService.GenerateModelsAsync(clientGuid, request.BlobPath, cancellationToken);
            return Ok(ApiResponse<List<ModelDto>>.SuccessResponse(list.ToList(), "Models generated."));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Insight model generate rejected.");
            return BadRequest(ApiResponse<object>.ErrorResponse(ex.Message));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "InsightEngine HTTP error during generate.");
            return StatusCode(
                StatusCodes.Status502BadGateway,
                ApiResponse<object>.ErrorResponse("InsightEngine request failed. See server logs for details."));
        }
    }

    [HttpPost("generate-from-blob")]
    public async Task<IActionResult> GenerateFromBlob([FromBody] GenerateFromBlobRequest req, CancellationToken cancellationToken)
    {
        if (req.ClientId == Guid.Empty)
            return BadRequest(ApiResponse<object>.ErrorResponse("ClientId is required."));

        if (string.IsNullOrWhiteSpace(req.BlobPath))
            return BadRequest(ApiResponse<object>.ErrorResponse("BlobPath is required."));

        if (!await CanAccessClientAsync(req.ClientId, cancellationToken))
            return StatusCode(403, ApiResponse<object>.ErrorResponse("You do not have access to this client."));

        try
        {
            var list = await _insightService.GenerateModelSuggestionsFromBlobAsync(req.ClientId, req.BlobPath, cancellationToken);
            return Ok(ApiResponse<List<ModelRecommendationDto>>.SuccessResponse(list.ToList(), "Recommendations generated."));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Insight model recommendation rejected.");
            return BadRequest(ApiResponse<object>.ErrorResponse(ex.Message));
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex, "Unsupported file type for sampling.");
            return BadRequest(ApiResponse<object>.ErrorResponse(ex.Message));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "InsightEngine HTTP error during recommend.");
            return StatusCode(
                StatusCodes.Status502BadGateway,
                ApiResponse<object>.ErrorResponse("InsightEngine request failed. See server logs for details."));
        }
    }

    [HttpPost("{modelId:guid}/select")]
    public async Task<IActionResult> Select(
        Guid modelId,
        [FromQuery(Name = "async")] bool queueAsync = false,
        CancellationToken cancellationToken = default)
    {
        var modelEntity = await _insightModelRepository.GetByIdAsync(modelId, cancellationToken);
        if (modelEntity == null)
            return NotFound(new SelectModelResponseDto { Message = "Model not found.", Queued = false, JobId = null, DatasetId = null, ReportId = null });
        if (!await CanAccessClientAsync(modelEntity.ClientId, cancellationToken))
            return StatusCode(403, new SelectModelResponseDto { Message = "You do not have access to this model.", Queued = false, JobId = null, DatasetId = null, ReportId = null });

        try
        {
            var dto = await _insightService.SelectModelAsync(modelId, queueAsync, cancellationToken);
            if (!dto.Queued && string.IsNullOrWhiteSpace(dto.DatasetId))
                return BadRequest(dto);

            return Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Insight model select rejected.");
            return BadRequest(new SelectModelResponseDto
            {
                Message = ex.Message,
                Queued = false,
                JobId = null,
                DatasetId = null,
                ReportId = null
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "InsightEngine HTTP error during select.");
            return StatusCode(StatusCodes.Status502BadGateway, new SelectModelResponseDto
            {
                Message = "InsightEngine request failed.",
                Queued = false,
                JobId = null,
                DatasetId = null,
                ReportId = null
            });
        }
    }

    [HttpGet("{clientId:guid}")]
    public async Task<IActionResult> ListForClient(Guid clientId, CancellationToken cancellationToken)
    {
        if (!await CanAccessClientAsync(clientId, cancellationToken))
            return StatusCode(403, new { success = false, message = "You do not have access to this client." });

        var list = await _insightService.GetModelsForClientAsync(clientId, cancellationToken);
        return Ok(list.Select(m => new
        {
            id = m.Id,
            templateId = m.TemplateId,
            confidence = m.Confidence ?? m.ResolveConfidence(),
            status = m.Status,
            datasetId = m.DatasetId,
            reportId = m.ReportId
        }).ToList());
    }

    public sealed class StoreAiDraftRequest
    {
        public Guid ClientId { get; set; }
        public AiModelResponse Ai { get; set; } = new();
    }

    /// <summary>
    /// Receives an AI model generation response, stores a DRAFT model (summary + TOM server-side), and returns UI-safe summary (no TOM).
    /// </summary>
    [HttpPost("ai/draft")]
    public async Task<IActionResult> StoreAiDraft([FromBody] StoreAiDraftRequest req, CancellationToken cancellationToken)
    {
        if (req.ClientId == Guid.Empty)
            return BadRequest(ApiResponse<object>.ErrorResponse("ClientId is required."));
        if (!await CanAccessClientAsync(req.ClientId, cancellationToken))
            return StatusCode(403, ApiResponse<object>.ErrorResponse("You do not have access to this client."));

        try
        {
            var dto = await _insightService.StoreAiDraftModelAsync(req.ClientId, req.Ai, cancellationToken);
            return Ok(ApiResponse<AiModelDraftSummaryDto>.SuccessResponse(dto, "Draft stored."));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "AI draft store rejected.");
            return BadRequest(ApiResponse<object>.ErrorResponse(ex.Message));
        }
    }

    public sealed class ApproveRequest
    {
        public Guid ModelId { get; set; }
        public Guid ClientId { get; set; }
    }

    /// <summary>
    /// Approves a model once (idempotent), stores consent, then triggers report generation.
    /// </summary>
    [HttpPost("approve")]
    public async Task<IActionResult> ApproveModel([FromBody] ApproveRequest req, CancellationToken cancellationToken)
    {
        if (req.ModelId == Guid.Empty)
            return BadRequest(ApiResponse<object>.ErrorResponse("ModelId is required."));
        if (req.ClientId == Guid.Empty)
            return BadRequest(ApiResponse<object>.ErrorResponse("ClientId is required."));
        if (!await CanAccessClientAsync(req.ClientId, cancellationToken))
            return StatusCode(403, ApiResponse<object>.ErrorResponse("You do not have access to this client."));

        try
        {
            var result = await _insightService.ApproveAiModelAsync(req.ModelId, req.ClientId, cancellationToken);
            return Ok(ApiResponse<SelectModelResponseDto>.SuccessResponse(result, "Approved."));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Model approval rejected.");
            return BadRequest(ApiResponse<object>.ErrorResponse(ex.Message));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Report generation failed (InsightEngine HTTP).");
            return StatusCode(
                StatusCodes.Status502BadGateway,
                ApiResponse<object>.ErrorResponse("Report generation request failed. See server logs for details."));
        }
    }
}
