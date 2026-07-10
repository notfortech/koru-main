using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.DTOs.Blueprints;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.AI;

/// <summary>
/// Long-running background worker that drains the BlueprintGenerationQueue.
/// Each generation job calls STBI-AgentHost, stores the Blueprint JSON artefact,
/// and updates the BlueprintGeneration record to Completed or Failed.
/// Uses IServiceScopeFactory to obtain scoped services (EF, repositories).
/// </summary>
public sealed class BlueprintGenerationBackgroundService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IBlueprintGenerationQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BlueprintGenerationBackgroundService> _logger;
    private readonly AgentHostOptions _agentHostOpts;

    public BlueprintGenerationBackgroundService(
        IBlueprintGenerationQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<BlueprintGenerationBackgroundService> logger,
        IOptions<AgentHostOptions> agentHostOptions)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _agentHostOpts = agentHostOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "BlueprintGenerationBackgroundService starting. " +
            "AgentHost:BaseUrl={BaseUrl} TimeoutSeconds={TimeoutSeconds} RetryCount={RetryCount} " +
            "EnableCircuitBreaker={EnableCircuitBreaker} CircuitFailureThreshold={CircuitFailureThreshold} " +
            "CircuitBreakDurationSeconds={CircuitBreakDurationSeconds}",
            MaskUrl(_agentHostOpts.BaseUrl),
            _agentHostOpts.TimeoutSeconds,
            _agentHostOpts.RetryCount,
            _agentHostOpts.EnableCircuitBreaker,
            _agentHostOpts.CircuitFailureThreshold,
            _agentHostOpts.CircuitBreakDurationSeconds);

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
                    "Unhandled exception in blueprint generation worker for GenerationId={GenerationId}.",
                    generationId);
            }
        }
    }

    private async Task ProcessAsync(Guid generationId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IBlueprintRepository>();
        var agentHost = scope.ServiceProvider.GetRequiredService<IAgentHostClient>();
        var storage = scope.ServiceProvider.GetRequiredService<IBlueprintStorageService>();

        var generation = await repo.GetGenerationByIdAsync(generationId, ct);
        if (generation is null)
        {
            _logger.LogWarning("Generation {GenerationId} not found — skipping.", generationId);
            return;
        }

        // Mark Processing
        generation.Status = BlueprintStatuses.Processing;
        generation.ProcessingStartedAt = DateTime.UtcNow;
        await repo.UpdateGenerationAsync(generation, ct);
        await repo.SaveChangesAsync(ct);

        _logger.LogInformation(
            "BlueprintGeneration.Started GenerationId={GenerationId} BlueprintId={BlueprintId}",
            generationId, generation.BlueprintId);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var request = DeserialiseRequest(generation.RequestPayloadJson);

            var response = await agentHost.GenerateBlueprintAsync(
                request, generation.RequestId, ct);
            sw.Stop();

            ValidateResponse(response);

            var blueprint = await repo.GetByIdAsync(generation.BlueprintId, ct)
                ?? throw new InvalidOperationException($"Blueprint {generation.BlueprintId} not found.");

            // Deactivate current active version
            var currentVersion = await repo.GetActiveVersionAsync(blueprint.Id, ct);
            if (currentVersion is not null)
            {
                currentVersion.IsActive = false;
                await repo.UpdateVersionAsync(currentVersion, ct);
            }

            var newVersionNumber = blueprint.VersionCount + 1;
            var version = new BlueprintVersion
            {
                Id = Guid.NewGuid(),
                BlueprintId = blueprint.Id,
                VersionNumber = newVersionNumber,
                PromptVersion = null,
                Confidence = response.ConfidenceFraction,
                GeneratedDate = DateTime.UtcNow,
                ExecutionTimeMs = response.ExecutionTimeMs,
                IsActive = true,
                CreatedBy = generation.CreatedBy
            };

            if (!string.IsNullOrWhiteSpace(response.BlueprintJson))
            {
                version.GeneratedJsonContent = response.BlueprintJson;
                version.JsonBlobPath = await storage.StoreJsonAsync(
                    blueprint.Id, newVersionNumber, response.BlueprintJson, ct);
            }

            if (!string.IsNullOrWhiteSpace(response.PdfDownloadUrl))
            {
                try
                {
                    var pdfBytes = await agentHost.DownloadPdfAsync(response.PdfDownloadUrl, ct);
                    if (pdfBytes is { Length: > 0 })
                    {
                        version.PdfBlobPath = await storage.StorePdfAsync(
                            blueprint.Id, newVersionNumber, pdfBytes, ct);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "BlueprintGeneration.PdfFetchEmpty GenerationId={GenerationId} PdfUrl={PdfUrl}",
                            generationId, response.PdfDownloadUrl);
                    }
                }
                catch (Exception ex)
                {
                    // PDF is a nice-to-have — the JSON contract is the primary artefact.
                    // Don't fail the whole generation if AgentHost's PDF can't be fetched.
                    _logger.LogWarning(ex,
                        "BlueprintGeneration.PdfFetchFailed GenerationId={GenerationId} PdfUrl={PdfUrl}",
                        generationId, response.PdfDownloadUrl);
                }
            }

            await repo.AddVersionAsync(version, ct);

            blueprint.VersionCount = newVersionNumber;
            await repo.UpdateAsync(blueprint, ct);

            generation.Status = BlueprintStatuses.Completed;
            generation.BlueprintVersionId = version.Id;
            generation.CompletedAt = DateTime.UtcNow;
            generation.ConfidenceScore = response.ConfidenceFraction;
            generation.Warnings = response.Warnings is { Count: > 0 }
                ? string.Join(";", response.Warnings)
                : null;

            await repo.UpdateGenerationAsync(generation, ct);
            await repo.SaveChangesAsync(ct);

            _logger.LogInformation(
                "BlueprintGeneration.Completed GenerationId={GenerationId} VersionNumber={VersionNumber} " +
                "DurationMs={DurationMs} Provider={Provider} Model={Model}",
                generationId, newVersionNumber,
                (long)(generation.CompletedAt!.Value - generation.ProcessingStartedAt!.Value).TotalMilliseconds,
                response.Provider, response.Model);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();

            _logger.LogError(ex,
                "BlueprintGeneration.Failed GenerationId={GenerationId} BlueprintId={BlueprintId} " +
                "ErrorType={ErrorType} DurationMs={DurationMs}",
                generationId, generation.BlueprintId,
                ex.GetType().Name,
                sw.ElapsedMilliseconds);

            var statusCode = ex is HttpRequestException httpEx ? (int?)httpEx.StatusCode : null;
            var errorJson = System.Text.Json.JsonSerializer.Serialize(new
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

            generation.Status = BlueprintStatuses.Failed;
            generation.ErrorMessage = errorJson.Length <= 2000 ? errorJson : errorJson[..2000];
            generation.CompletedAt = DateTime.UtcNow;

            try
            {
                await repo.UpdateGenerationAsync(generation, ct);
                await repo.SaveChangesAsync(ct);
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
            var repo = scope.ServiceProvider.GetRequiredService<IBlueprintRepository>();

            var stuck = await repo.GetPendingGenerationsAsync(ct);
            var count = 0;
            foreach (var g in stuck)
            {
                await _queue.EnqueueAsync(g.Id, ct);
                count++;
            }

            if (count > 0)
                _logger.LogInformation("Re-queued {Count} stuck blueprint generation(s) on startup.", count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not re-queue stuck generations on startup.");
        }
    }

    private static GenerateBlueprintRequest DeserialiseRequest(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("Generation record has no RequestPayloadJson.");

        return JsonSerializer.Deserialize<GenerateBlueprintRequest>(json, JsonOptions)
            ?? throw new InvalidOperationException("RequestPayloadJson could not be deserialised.");
    }

    private static string MaskUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "(not configured)";
        try
        {
            var uri = new Uri(url);
            return $"{uri.Scheme}://{uri.Host}";
        }
        catch
        {
            return "(invalid url)";
        }
    }

    private static void ValidateResponse(BlueprintGenerationResponse response)
    {
        if (response.Blueprint is null)
            throw new InvalidOperationException("AgentHost response contains no Blueprint document.");
    }
}
