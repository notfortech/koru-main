using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.ReportDesigner;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.Infrastructure.AI;

/// <summary>
/// Long-running background worker that drains the SchemaModelMatchQueue. Each job calls
/// IReportMatchService.MatchAsync (the same call ReportDesignerController used to make
/// synchronously, blocking the request for up to ~330s when it escalates to AI semantic
/// matching), then updates the SchemaModelMatch record to Completed or Failed. Uses
/// IServiceScopeFactory for scoped services (EF) -- same pattern as
/// ReportModelGenerationBackgroundService. No AI-credit consumption here -- the synchronous
/// MatchAsync controller action never charged credits for this call either.
/// </summary>
public sealed class SchemaModelMatchBackgroundService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ISchemaModelMatchQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SchemaModelMatchBackgroundService> _logger;

    public SchemaModelMatchBackgroundService(
        ISchemaModelMatchQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<SchemaModelMatchBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SchemaModelMatchBackgroundService starting.");

        // On restart, re-queue any matches that were stuck in Pending/Processing.
        await RequeueStuckMatchesAsync(stoppingToken);

        await foreach (var matchId in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(matchId, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex,
                    "Unhandled exception in schema model match worker for MatchId={MatchId}.",
                    matchId);
            }
        }
    }

    private async Task ProcessAsync(Guid matchId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reportMatchService = scope.ServiceProvider.GetRequiredService<IReportMatchService>();

        var match = await db.SchemaModelMatches.FirstOrDefaultAsync(m => m.Id == matchId, ct);
        if (match is null)
        {
            _logger.LogWarning("SchemaModelMatch {MatchId} not found — skipping.", matchId);
            return;
        }

        match.Status = SchemaModelMatchStatuses.Processing;
        match.ProcessingStartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "SchemaModelMatch.Started MatchId={MatchId} ClientId={ClientId}",
            matchId, match.ClientId);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var request = JsonSerializer.Deserialize<ReportMatchRequest>(match.RequestPayloadJson, JsonOptions)
                ?? throw new InvalidOperationException("SchemaModelMatch.RequestPayloadJson could not be deserialised.");

            var result = await reportMatchService.MatchAsync(match.ClientId, request.Schema, ct);
            sw.Stop();

            match.ResponseJson = JsonSerializer.Serialize(result, JsonOptions);
            match.Status = SchemaModelMatchStatuses.Completed;
            match.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "SchemaModelMatch.Completed MatchId={MatchId} DurationMs={DurationMs}",
                matchId, sw.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();

            _logger.LogError(ex,
                "SchemaModelMatch.Failed MatchId={MatchId} ClientId={ClientId} " +
                "ErrorType={ErrorType} DurationMs={DurationMs}",
                matchId, match.ClientId, ex.GetType().Name, sw.ElapsedMilliseconds);

            var statusCode = ex is HttpRequestException httpEx ? (int?)httpEx.StatusCode : null;
            var errorJson = JsonSerializer.Serialize(new
            {
                type = ex.GetType().Name,
                status = statusCode,
                msg = ex.Message.Length > 200 ? ex.Message[..200] : ex.Message,
                correlationId = match.RequestId,
                durationMs = sw.ElapsedMilliseconds,
                inner = ex.InnerException?.Message is { } im
                    ? (im.Length > 100 ? im[..100] : im)
                    : null
            });

            match.Status = SchemaModelMatchStatuses.Failed;
            match.ErrorMessage = errorJson.Length <= 2000 ? errorJson : errorJson[..2000];
            match.CompletedAt = DateTime.UtcNow;

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx,
                    "Failed to persist Failed status for MatchId={MatchId}.", matchId);
            }
        }
    }

    private async Task RequeueStuckMatchesAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var stuck = await db.SchemaModelMatches
                .Where(m => m.Status == SchemaModelMatchStatuses.Pending || m.Status == SchemaModelMatchStatuses.Processing)
                .Select(m => m.Id)
                .ToListAsync(ct);

            foreach (var id in stuck)
                await _queue.EnqueueAsync(id, ct);

            if (stuck.Count > 0)
                _logger.LogInformation("Re-queued {Count} stuck schema model match(es) on startup.", stuck.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not re-queue stuck schema model matches on startup.");
        }
    }
}
