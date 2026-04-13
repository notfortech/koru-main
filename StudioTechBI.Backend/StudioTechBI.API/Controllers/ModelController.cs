using System.Net.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.DTOs.Insight;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.API.Controllers;

[ApiController]
[Route("api/models")]
[Authorize]
public class ModelController : ControllerBase
{
    private readonly IInsightService _insightService;
    private readonly ILogger<ModelController> _logger;

    public ModelController(IInsightService insightService, ILogger<ModelController> logger)
    {
        _insightService = insightService;
        _logger = logger;
    }

    public sealed class GenerateModelsApiRequest
    {
        public Guid ClientId { get; set; }
        /// <summary>Optional full blob path; otherwise the latest .xlsx under {client}/accounting/created/ is used.</summary>
        public string? BlobPath { get; set; }
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateModelsApiRequest request, CancellationToken cancellationToken)
    {
        if (request.ClientId == Guid.Empty)
            return BadRequest(ApiResponse<object>.ErrorResponse("ClientId is required."));

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
    public async Task<IActionResult> Select(Guid modelId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _insightService.SelectModelAsync(modelId, cancellationToken);
            if (!result.Success)
                return BadRequest(ApiResponse<OrchestratorResultDto>.ErrorResponse(result.Message ?? "Orchestrator did not succeed.", null));

            return Ok(ApiResponse<OrchestratorResultDto>.SuccessResponse(result, "Model selected and orchestration completed."));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Insight model select rejected.");
            return BadRequest(ApiResponse<object>.ErrorResponse(ex.Message));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "InsightEngine HTTP error during select.");
            return StatusCode(
                StatusCodes.Status502BadGateway,
                ApiResponse<object>.ErrorResponse("InsightEngine request failed. See server logs for details."));
        }
    }

    [HttpGet("{clientId:guid}")]
    public async Task<IActionResult> ListForClient(Guid clientId, CancellationToken cancellationToken)
    {
        var list = await _insightService.GetModelsForClientAsync(clientId, cancellationToken);
        return Ok(ApiResponse<List<ModelDto>>.SuccessResponse(list.ToList()));
    }
}
