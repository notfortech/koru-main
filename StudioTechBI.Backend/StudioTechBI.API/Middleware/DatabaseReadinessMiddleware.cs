using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using StudioTechBI.API.Services;
using StudioTechBI.Application.DTOs.Common;

namespace StudioTechBI.API.Middleware;

/// <summary>
/// Returns 503 until the database is migrated and ready. Prevents 500s when clients hit /api
/// before <see cref="StartupDbTasksHostedService"/> finishes (Azure Linux startup race).
/// </summary>
public sealed class DatabaseReadinessMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public DatabaseReadinessMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, DatabaseReadinessState readiness, IWebHostEnvironment env)
    {
        var path = context.Request.Path.Value ?? "";
        if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) || readiness.IsDatabaseReady)
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.RetryAfter = "3";
        context.Response.ContentType = "application/json; charset=utf-8";

        var message = "Database is not ready yet. Migrations or seeding are still running; retry in a few seconds.";

        var body = ApiResponse<object>.ErrorResponse(
            message,
            env.IsDevelopment() ? new List<string> { "ServiceStartup" } : null);

        await context.Response.WriteAsync(JsonSerializer.Serialize(body, Json));
    }
}
