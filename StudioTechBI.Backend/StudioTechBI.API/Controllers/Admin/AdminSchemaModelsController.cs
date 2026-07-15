using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.API.Controllers.Admin;

/// <summary>
/// Support review queue for AI-proposed SchemaModels. When the AI matching pipeline
/// (see ReportMatchService.TryEscalateToAiAsync) finds no good fit in the existing
/// directory, it creates a new SchemaModel with ReviewStatus = PendingReview — excluded
/// from other clients' matching until a support user approves or rejects it here.
/// </summary>
[ApiController]
[Route("api/admin/schema-models")]
[Authorize(Roles = "Admin,SuperAdmin,OperationsAdmin,SupportAdmin")]
public class AdminSchemaModelsController : ControllerBase
{
    private readonly ISchemaModelService _schemaModelService;

    public AdminSchemaModelsController(ISchemaModelService schemaModelService)
    {
        _schemaModelService = schemaModelService;
    }

    /// <summary>GET /api/admin/schema-models/pending — AI-proposed models awaiting review.</summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingAsync(CancellationToken cancellationToken)
    {
        var models = await _schemaModelService.GetPendingReviewAsync(cancellationToken);
        return Ok(ApiResponse<object>.SuccessResponse(models, $"{models.Count} model(s) pending review."));
    }

    /// <summary>POST /api/admin/schema-models/{id}/approve — makes the model eligible for other clients' matching.</summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> ApproveAsync(Guid id, CancellationToken cancellationToken)
    {
        var approvedBy = User.FindFirstValue(ClaimTypes.Email) ?? "unknown";
        var model = await _schemaModelService.ApproveAsync(id, approvedBy, cancellationToken);
        if (model is null)
            return NotFound(ApiResponse<object>.ErrorResponse("Schema model not found or not pending review."));

        return Ok(ApiResponse<object>.SuccessResponse(model, "Schema model approved."));
    }

    /// <summary>POST /api/admin/schema-models/{id}/reject — keeps the model out of matching permanently.</summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> RejectAsync(Guid id, CancellationToken cancellationToken)
    {
        var rejectedBy = User.FindFirstValue(ClaimTypes.Email) ?? "unknown";
        var model = await _schemaModelService.RejectAsync(id, rejectedBy, cancellationToken);
        if (model is null)
            return NotFound(ApiResponse<object>.ErrorResponse("Schema model not found or not pending review."));

        return Ok(ApiResponse<object>.SuccessResponse(model, "Schema model rejected."));
    }
}
