using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.API.Controllers;

/// <summary>
/// Read access to the reference SchemaModel library — the industry data shapes
/// (Accounting, Finance, Real Estate, Transport, NDIS, ...) that a connecting client's
/// schema is matched against, and the Dashboard Templates attached to each.
/// </summary>
[ApiController]
[Route("api/schema-models")]
[Authorize]
public class SchemaModelsController : ControllerBase
{
    private readonly ISchemaModelService _schemaModelService;

    public SchemaModelsController(ISchemaModelService schemaModelService)
    {
        _schemaModelService = schemaModelService;
    }

    /// <summary>GET /api/schema-models — every reference model in the library, with its fields and attached template ids.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        var models = await _schemaModelService.GetAllAsync(cancellationToken);
        return Ok(ApiResponse<object>.SuccessResponse(models, $"{models.Count} schema model(s)."));
    }

    /// <summary>GET /api/schema-models/{id} — a single reference model, with its full field list and attached template ids.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var model = await _schemaModelService.GetByIdAsync(id, cancellationToken);
        if (model is null)
            return NotFound(ApiResponse<object>.ErrorResponse("Schema model not found."));

        return Ok(ApiResponse<object>.SuccessResponse(model, "OK"));
    }
}
