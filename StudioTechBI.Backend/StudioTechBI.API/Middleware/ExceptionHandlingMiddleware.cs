using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using StudioTechBI.Application.DTOs.Common;

namespace StudioTechBI.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly bool _isProduction;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _isProduction = env.IsProduction();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var response = ApiResponse<object>.ErrorResponse(
            _isProduction
                ? "An error occurred while processing your request."
                : "An error occurred while processing your request.",
            _isProduction ? null : new List<string> { exception.Message }
        );

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
    }
}
