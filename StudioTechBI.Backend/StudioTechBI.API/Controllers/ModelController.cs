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
        public Guid ClientId { get; set; }
        /// <summary>Optional full blob path; otherwise the latest file under accounting/created/ is used.</summary>
        public string? BlobPath { get; set; }
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateModelsApiRequest request, CancellationToken cancellationToken)
    {
        if (request.ClientId == Guid.Empty)
            return BadRequest(ApiResponse<object>.ErrorResponse("ClientId is required."));

        if (!await CanAccessClientAsync(request.ClientId, cancellationToken))
            return StatusCode(403, ApiResponse<object>.ErrorResponse("You do not have access to this client."));

        try
        {
            var list = await _insightService.GenerateModelsAsync(request.ClientId, request.BlobPath, cancellationToken);
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
}
