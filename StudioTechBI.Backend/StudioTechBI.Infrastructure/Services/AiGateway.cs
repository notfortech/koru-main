using System.Text.Json;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.Blueprints;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Services;

/// <summary>
/// Orchestrates Blueprint generation: validates the request, finds-or-creates the
/// Blueprint aggregate, persists the generation job, queues it for background
/// processing, and returns status to the controller.
/// </summary>
public sealed class AiGateway : IAiGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IBlueprintRepository _repo;
    private readonly IBlueprintGenerationQueue _queue;
    private readonly IBlueprintStorageService _storage;
    private readonly ILogger<AiGateway> _logger;

    public AiGateway(
        IBlueprintRepository repo,
        IBlueprintGenerationQueue queue,
        IBlueprintStorageService storage,
        ILogger<AiGateway> logger)
    {
        _repo = repo;
        _queue = queue;
        _storage = storage;
        _logger = logger;
    }

    public async Task<BlueprintGenerationJobDto> QueueBlueprintGenerationAsync(
        GenerateBlueprintRequest request,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        var tenantId = request.TenantId ?? Guid.Empty;
        var clientId = request.ClientId ?? Guid.Empty;

        // Find or create the Blueprint aggregate
        var blueprint = await _repo.GetByProjectAsync(
            tenantId, clientId, request.ProjectId, cancellationToken);

        if (blueprint is null)
        {
            blueprint = new Blueprint
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ClientId = clientId,
                ProjectId = request.ProjectId,
                Industry = request.Industry,
                KnowledgePack = request.KnowledgePack,
                Status = BlueprintStatuses.Active,
                VersionCount = 0,
                CreatedBy = createdBy
            };
            await _repo.AddAsync(blueprint, cancellationToken);
        }
        else
        {
            // Keep Industry / KnowledgePack current on regeneration requests
            blueprint.Industry = request.Industry;
            blueprint.KnowledgePack = request.KnowledgePack;
            await _repo.UpdateAsync(blueprint, cancellationToken);
        }

        var correlationId = Guid.NewGuid().ToString();
        var generation = new BlueprintGeneration
        {
            Id = Guid.NewGuid(),
            BlueprintId = blueprint.Id,
            RequestId = correlationId,
            Status = BlueprintStatuses.Pending,
            RequestPayloadJson = JsonSerializer.Serialize(request, JsonOptions),
            GeneratePdf = false,
            GenerateJson = true,
            CreatedBy = createdBy
        };

        await _repo.AddGenerationAsync(generation, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);

        await _queue.EnqueueAsync(generation.Id, cancellationToken);

        _logger.LogInformation(
            "BlueprintGeneration.Queued GenerationId={GenerationId} BlueprintId={BlueprintId} " +
            "TenantId={TenantId} ClientId={ClientId} ProjectId={ProjectId} CorrelationId={CorrelationId}",
            generation.Id, blueprint.Id, request.TenantId, request.ClientId,
            request.ProjectId, correlationId);

        return MapToJobDto(generation);
    }

    public async Task<BlueprintGenerationJobDto?> GetGenerationStatusAsync(
        Guid generationId,
        CancellationToken cancellationToken = default)
    {
        var generation = await _repo.GetGenerationByIdAsync(generationId, cancellationToken);
        return generation is null ? null : MapToJobDto(generation);
    }

    public async Task<(IEnumerable<BlueprintDto> Items, int TotalCount)> GetBlueprintsAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await _repo.GetPagedByTenantAsync(tenantId, page, pageSize, cancellationToken);
        return (items.Select(MapToDto), total);
    }

    public async Task<BlueprintDto?> GetBlueprintAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var blueprint = await _repo.GetByIdAsync(id, cancellationToken);
        return blueprint is null ? null : MapToDto(blueprint);
    }

    public async Task<Stream?> GetBlueprintPdfAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var version = await _repo.GetActiveVersionAsync(id, cancellationToken);
        if (version?.PdfBlobPath is null) return null;
        return await _storage.GetPdfAsync(version.PdfBlobPath, cancellationToken);
    }

    public async Task<string?> GetBlueprintJsonAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var version = await _repo.GetActiveVersionAsync(id, cancellationToken);
        if (version?.JsonBlobPath is null) return null;

        await using var stream = await _storage.GetJsonAsync(version.JsonBlobPath, cancellationToken);
        if (stream is null) return null;

        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    public async Task DeleteBlueprintAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var blueprint = await _repo.GetByIdAsync(id, cancellationToken);
        if (blueprint is null) return;

        await _repo.DeleteAsync(blueprint, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Soft-deleted Blueprint {BlueprintId}.", id);
    }

    // ── Mapping ────────────────────────────────────────────────────────────────

    private static BlueprintDto MapToDto(Blueprint b)
    {
        var activeVersion = b.Versions.FirstOrDefault(v => v.IsActive);
        return new BlueprintDto
        {
            Id = b.Id,
            TenantId = b.TenantId,
            ClientId = b.ClientId,
            ProjectId = b.ProjectId,
            Industry = b.Industry,
            KnowledgePack = b.KnowledgePack,
            Status = b.Status,
            VersionCount = b.VersionCount,
            CreatedAt = b.CreatedAt,
            UpdatedAt = b.UpdatedAt,
            CreatedBy = b.CreatedBy,
            ActiveVersion = activeVersion is null ? null : new BlueprintVersionDto
            {
                Id = activeVersion.Id,
                VersionNumber = activeVersion.VersionNumber,
                PromptVersion = activeVersion.PromptVersion,
                Confidence = activeVersion.Confidence,
                GeneratedDate = activeVersion.GeneratedDate,
                ExecutionTimeMs = activeVersion.ExecutionTimeMs,
                HasJson = activeVersion.JsonBlobPath is not null,
                HasPdf = activeVersion.PdfBlobPath is not null,
                GeneratedJsonContent = activeVersion.GeneratedJsonContent
            }
        };
    }

    private static BlueprintGenerationJobDto MapToJobDto(BlueprintGeneration g) => new()
    {
        GenerationId = g.Id,
        BlueprintId = g.BlueprintId,
        RequestId = g.RequestId,
        Status = g.Status,
        ConfidenceScore = g.ConfidenceScore,
        Warnings = g.Warnings is not null
            ? g.Warnings.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList()
            : null,
        ErrorMessage = g.ErrorMessage,
        CreatedAt = g.CreatedAt,
        ProcessingStartedAt = g.ProcessingStartedAt,
        CompletedAt = g.CompletedAt,
        BlueprintVersionId = g.BlueprintVersionId
    };
}
