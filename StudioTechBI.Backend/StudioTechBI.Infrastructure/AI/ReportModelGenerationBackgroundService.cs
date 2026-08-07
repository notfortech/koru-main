using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.Constants;
using StudioTechBI.Application.DTOs.ReportDesigner;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.Infrastructure.AI;

/// <summary>
/// Long-running background worker that drains the ReportModelGenerationQueue. Each job calls the
/// Report Designer AI model (the same call ReportDesignerController used to make synchronously,
/// blocking the request for up to ~330s), then updates the ReportModelGeneration record to
/// Completed or Failed. Uses IServiceScopeFactory for scoped services (EF) -- same pattern as
/// ReportValidationBackgroundService.
///
/// AI credits are consumed here, only after the call actually succeeds -- never on enqueue and
/// never on failure, matching this codebase's established "don't charge for a failed AI response"
/// principle. The credit CHECK (does the client have enough balance) still happens synchronously
/// in the controller before enqueueing, so a client without credits gets an immediate 402 rather
/// than waiting for a queued job to fail.
/// </summary>
public sealed class ReportModelGenerationBackgroundService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IReportModelGenerationQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReportModelGenerationBackgroundService> _logger;

    public ReportModelGenerationBackgroundService(
        IReportModelGenerationQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<ReportModelGenerationBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReportModelGenerationBackgroundService starting.");

        // On restart, re-queue any generations that were stuck in Pending/Processing.
        await RequeueStuckGenerationsAsync(stoppingToken);

        await foreach (var generationId in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(generationId, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex,
                    "Unhandled exception in report model generation worker for GenerationId={GenerationId}.",
                    generationId);
            }
        }
    }

    private async Task ProcessAsync(Guid generationId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reportDesignerClient = scope.ServiceProvider.GetRequiredService<IReportDesignerClient>();
        var agentHostClient = scope.ServiceProvider.GetRequiredService<IAgentHostClient>();
        var localCredits = scope.ServiceProvider.GetRequiredService<ILocalCreditLedgerService>();

        var generation = await db.ReportModelGenerations.FirstOrDefaultAsync(g => g.Id == generationId, ct);
        if (generation is null)
        {
            _logger.LogWarning("ReportModelGeneration {GenerationId} not found — skipping.", generationId);
            return;
        }

        generation.Status = ReportModelGenerationStatuses.Processing;
        generation.ProcessingStartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "ReportModelGeneration.Started GenerationId={GenerationId} ClientId={ClientId}",
            generationId, generation.ClientId);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var request = JsonSerializer.Deserialize<GenerateReportModelRequest>(generation.RequestPayloadJson, JsonOptions)
                ?? throw new InvalidOperationException("ReportModelGeneration.RequestPayloadJson could not be deserialised.");

            var response = await reportDesignerClient.GenerateReportModelAsync(request, generation.RequestId, ct);
            sw.Stop();

            // Consume credits only now that the call has actually succeeded — mirrors what
            // ReportDesignerController used to do synchronously right after a successful call.
            await agentHostClient.ConsumeCreditAsync(
                generation.ClientId, ReportModelGenerationConstants.Feature, generation.RequestId, response.DurationMs, ct);
            await localCredits.ConsumeAsync(
                generation.ClientId, ReportModelGenerationConstants.CreditCost, ReportModelGenerationConstants.Feature, ct);

            generation.ResponseJson = JsonSerializer.Serialize(response, JsonOptions);
            generation.Status = ReportModelGenerationStatuses.Completed;
            generation.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "ReportModelGeneration.Completed GenerationId={GenerationId} DurationMs={DurationMs}",
                generationId, sw.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();

            _logger.LogError(ex,
                "ReportModelGeneration.Failed GenerationId={GenerationId} ClientId={ClientId} " +
                "ErrorType={ErrorType} DurationMs={DurationMs}",
                generationId, generation.ClientId, ex.GetType().Name, sw.ElapsedMilliseconds);

            var statusCode = ex is HttpRequestException httpEx ? (int?)httpEx.StatusCode : null;
            var errorJson = JsonSerializer.Serialize(new
            {
                type = ex.GetType().Name,
                status = statusCode,
                msg = ex.Message.Length > 200 ? ex.Message[..200] : ex.Message,
                correlationId = generation.RequestId,
                durationMs = sw.ElapsedMilliseconds,
                inner = ex.InnerException?.Message is { } im
                    ? (im.Length > 100 ? im[..100] : im)
                    : null
            });

            generation.Status = ReportModelGenerationStatuses.Failed;
            generation.ErrorMessage = errorJson.Length <= 2000 ? errorJson : errorJson[..2000];
            generation.CompletedAt = DateTime.UtcNow;

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx,
                    "Failed to persist Failed status for GenerationId={GenerationId}.", generationId);
            }
        }
    }

    private async Task RequeueStuckGenerationsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var stuck = await db.ReportModelGenerations
                .Where(g => g.Status == ReportModelGenerationStatuses.Pending || g.Status == ReportModelGenerationStatuses.Processing)
                .Select(g => g.Id)
                .ToListAsync(ct);

            foreach (var id in stuck)
                await _queue.EnqueueAsync(id, ct);

            if (stuck.Count > 0)
                _logger.LogInformation("Re-queued {Count} stuck report model generation(s) on startup.", stuck.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not re-queue stuck report model generations on startup.");
        }
    }
}
