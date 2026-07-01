using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.Blueprints;
using StudioTechBI.Application.Interfaces;
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

    public BlueprintGenerationBackgroundService(
        IBlueprintGenerationQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<BlueprintGenerationBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Blueprint generation background service started.");

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
            "Processing blueprint generation. GenerationId={GenerationId} BlueprintId={BlueprintId}",
            generationId, generation.BlueprintId);

        try
        {
            var request = DeserialiseRequest(generation.RequestPayloadJson);

            var response = await agentHost.GenerateBlueprintAsync(
                request, generation.RequestId, ct);

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
                PromptVersion = response.Diagnostics?.PromptPackVersion,
                Confidence = response.Confidence,
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

            await repo.AddVersionAsync(version, ct);

            blueprint.VersionCount = newVersionNumber;
            await repo.UpdateAsync(blueprint, ct);

            generation.Status = BlueprintStatuses.Completed;
            generation.BlueprintVersionId = version.Id;
            generation.CompletedAt = DateTime.UtcNow;
            generation.ConfidenceScore = response.Confidence;
            generation.Warnings = response.Warnings is { Count: > 0 }
                ? string.Join(";", response.Warnings)
                : null;

            await repo.UpdateGenerationAsync(generation, ct);
            await repo.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Blueprint generation completed. GenerationId={GenerationId} VersionNumber={Version} Provider={Provider} Model={Model} LatencyMs={LatencyMs}",
                generationId, newVersionNumber, response.Provider, response.Model,
                response.Diagnostics?.ProviderLatencyMs);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Blueprint generation failed. GenerationId={GenerationId} BlueprintId={BlueprintId}",
                generationId, generation.BlueprintId);

            generation.Status = BlueprintStatuses.Failed;
            var fullError = ex.ToString();
            generation.ErrorMessage = fullError.Length <= 2000 ? fullError : fullError[..2000];
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

    private static void ValidateResponse(BlueprintGenerationResponse response)
    {
        if (response.BlueprintId == Guid.Empty)
            throw new InvalidOperationException("AgentHost response is missing BlueprintId.");

        if (response.Blueprint is null)
            throw new InvalidOperationException("AgentHost response contains no Blueprint document.");
    }
}
