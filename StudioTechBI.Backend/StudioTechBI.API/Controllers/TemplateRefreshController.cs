using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.DTOs.TemplateRefresh;
using StudioTechBI.Application.DTOs.Templates;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.API.Controllers;

/// <summary>
/// New, standalone endpoint: match a client's uploaded schema against the manually-curated
/// template catalog (ITemplateMatchingService, unchanged), then rebind the matched template's
/// published Power BI dataset into that client's workspace. Does not touch ReportDesignerController
/// or its AI-generation publish path — that path builds a brand-new semantic model per client;
/// this one reuses an existing, manually-authored template (see BLUEPRINT_TEMPLATE_FORMAT.md in
/// stbi_transformers for why the two stay separate).
/// </summary>
[ApiController]
[Route("api/template-refresh")]
[Authorize]
public class TemplateRefreshController : ControllerBase
{
    private readonly ITemplateRefreshService _templateRefresh;
    private readonly ILogger<TemplateRefreshController> _logger;

    public TemplateRefreshController(ITemplateRefreshService templateRefresh, ILogger<TemplateRefreshController> logger)
    {
        _templateRefresh = templateRefresh;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/template-refresh/match
    /// Scores an already-uploaded client file against the template catalog. ReadyForInstantRefresh
    /// is true only when every required column was present on the top match — callers should
    /// confirm the column mapping with the user before calling /rebind otherwise.
    /// </summary>
    [HttpPost("match")]
    public async Task<IActionResult> MatchAsync([FromBody] TemplateMatchRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ClientCode) && !request.UseSelectedClient)
            return BadRequest(ApiResponse<object>.ErrorResponse("clientCode is required."));

        try
        {
            var result = await _templateRefresh.MatchAsync(request, cancellationToken);
            return Ok(ApiResponse<TemplateRefreshMatchResponse>.SuccessResponse(result,
                result.ReadyForInstantRefresh
                    ? $"Matched '{result.Match.Templates.FirstOrDefault()?.Name}' — ready to refresh instantly."
                    : "Best match found, but review the column mapping before refreshing."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse(ex.Message));
        }
    }

    /// <summary>
    /// POST /api/template-refresh/rebind
    /// Clones the matched template's published dataset into the client's own Power BI workspace,
    /// points it at the client's data, and triggers a refresh.
    /// </summary>
    [HttpPost("rebind")]
    public async Task<IActionResult> RebindAsync([FromBody] TemplateRefreshRebindRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ClientCode))
            return BadRequest(ApiResponse<object>.ErrorResponse("clientCode is required."));
        if (request.TemplateId == Guid.Empty)
            return BadRequest(ApiResponse<object>.ErrorResponse("templateId is required."));
        if (string.IsNullOrWhiteSpace(request.SourceFilePath))
            return BadRequest(ApiResponse<object>.ErrorResponse("sourceFilePath is required."));

        try
        {
            var result = await _templateRefresh.RebindAsync(request, cancellationToken);
            return Ok(ApiResponse<TemplateRefreshRebindResponse>.SuccessResponse(result, "Report refreshed."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse(ex.Message));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "TemplateRefresh.RebindFailed ClientCode={ClientCode} TemplateId={TemplateId}",
                request.ClientCode, request.TemplateId);
            return StatusCode((int)(ex.StatusCode ?? System.Net.HttpStatusCode.BadGateway),
                ApiResponse<object>.ErrorResponse(ex.Message));
        }
    }
}
