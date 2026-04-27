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
            var root = Unwrap(ex);
            _logger.LogError(root, "An unhandled exception occurred");
            await HandleExceptionAsync(context, root);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var detail = exception.InnerException?.Message ?? exception.Message;
        var response = ApiResponse<object>.ErrorResponse(
            "An error occurred while processing your request.",
            _isProduction ? null : new List<string> { detail }
        );

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
    }

    private static Exception Unwrap(Exception ex)
    {
        var e = ex;
        for (var i = 0; i < 10; i++)
        {
            if (e is System.Reflection.TargetInvocationException t && t.InnerException != null)
            {
                e = t.InnerException;
                continue;
            }
            if (e is AggregateException a && a.InnerExceptions.Count > 0)
            {
                e = a.Flatten().InnerExceptions[0];
                continue;
            }
            break;
        }
        return e;
    }
}
