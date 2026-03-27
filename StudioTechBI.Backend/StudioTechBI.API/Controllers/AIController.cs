using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.Application.DTOs.AI;
using StudioTechBI.Application.Services;

namespace StudioTechBI.API.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class AIController : ControllerBase
{
    private readonly AllInsightsService _service;
    private readonly ILogger<AIController> _logger;

    public AIController(AllInsightsService service, ILogger<AIController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpPost("insights")]
    public async Task<IActionResult> GetInsights([FromBody] InsightRequest request)
    {
        _logger.LogInformation("Generating AI insights for period type {PeriodType}.", request.PeriodType);
        var result = await _service.GetInsights(
            request.PeriodType,
            request.Period,
            request.ClientCode);

        return Ok(result);
    }
}
