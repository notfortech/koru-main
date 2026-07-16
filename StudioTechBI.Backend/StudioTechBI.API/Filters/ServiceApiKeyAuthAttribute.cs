using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.Models;

namespace StudioTechBI.API.Filters;

/// <summary>
/// Validates the X-Service-Api-Key header against Security:StbiTransformersApiKey for
/// machine-to-machine endpoints that aren't admin-JWT-gated (currently just
/// POST /api/internal/ai-boundary-audit-log). Not a general-purpose auth scheme — scoped
/// deliberately narrow, one shared secret for one caller (stbi_transformers), since that's the
/// only inbound machine caller this backend has today.
/// </summary>
public class ServiceApiKeyAuthAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var options = context.HttpContext.RequestServices
            .GetRequiredService<IOptionsMonitor<SecurityOptions>>().CurrentValue;

        if (string.IsNullOrWhiteSpace(options.StbiTransformersApiKey))
        {
            context.Result = new ObjectResult(
                ApiResponse<object>.ErrorResponse("Service API key auth is not configured on this backend."))
            { StatusCode = StatusCodes.Status503ServiceUnavailable };
            return;
        }

        var provided = context.HttpContext.Request.Headers["X-Service-Api-Key"].ToString();
        if (string.IsNullOrEmpty(provided) || provided != options.StbiTransformersApiKey)
        {
            context.Result = new ObjectResult(ApiResponse<object>.ErrorResponse("Invalid or missing X-Service-Api-Key."))
            { StatusCode = StatusCodes.Status401Unauthorized };
            return;
        }

        await next();
    }
}
