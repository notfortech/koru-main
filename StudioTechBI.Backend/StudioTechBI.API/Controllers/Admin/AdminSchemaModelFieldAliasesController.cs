using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.API.Controllers.Admin;

/// <summary>
/// Support review queue for learned column aliases (see SchemaModelFieldAlias entity and
/// ReportMatchService.WriteAliasesAsync for how a row gets here — an AI match's per-field
/// mapping that isn't already an exact name match). Nothing here is auto-trusted: an alias
/// only affects matching for other clients once approved. A wrong one has a much larger blast
/// radius than a wrong AI-proposed SchemaModel (see AdminSchemaModelsController) — it silently
/// rewires matching for every future client with a similarly-named column, not just one
/// client's own draft — so review here matters more, not less.
/// </summary>
[ApiController]
[Route("api/admin/schema-model-field-aliases")]
[Authorize(Roles = "Admin,SuperAdmin,OperationsAdmin,SupportAdmin")]
public class AdminSchemaModelFieldAliasesController : ControllerBase
{
    private readonly ISchemaModelFieldAliasService _aliasService;

    public AdminSchemaModelFieldAliasesController(ISchemaModelFieldAliasService aliasService)
    {
        _aliasService = aliasService;
    }

    /// <summary>GET /api/admin/schema-model-field-aliases/pending — learned aliases awaiting review.</summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingAsync(CancellationToken cancellationToken)
    {
        var aliases = await _aliasService.GetPendingReviewAsync(cancellationToken);
        return Ok(ApiResponse<object>.SuccessResponse(aliases, $"{aliases.Count} alias(es) pending review."));
    }

    /// <summary>POST /api/admin/schema-model-field-aliases/{id}/approve — trusts the alias for future matching.</summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> ApproveAsync(Guid id, CancellationToken cancellationToken)
    {
        var approvedBy = User.FindFirstValue(ClaimTypes.Email) ?? "unknown";
        var alias = await _aliasService.ApproveAsync(id, approvedBy, cancellationToken);
        if (alias is null)
            return NotFound(ApiResponse<object>.ErrorResponse("Alias not found or not pending review."));

        return Ok(ApiResponse<object>.SuccessResponse(alias, "Alias approved."));
    }

    /// <summary>POST /api/admin/schema-model-field-aliases/{id}/reject — keeps the alias out of matching permanently.</summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> RejectAsync(Guid id, CancellationToken cancellationToken)
    {
        var rejectedBy = User.FindFirstValue(ClaimTypes.Email) ?? "unknown";
        var alias = await _aliasService.RejectAsync(id, rejectedBy, cancellationToken);
        if (alias is null)
            return NotFound(ApiResponse<object>.ErrorResponse("Alias not found or not pending review."));

        return Ok(ApiResponse<object>.SuccessResponse(alias, "Alias rejected."));
    }
}
