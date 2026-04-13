using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.DTOs.Insight;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;
using StudioTechBI.Application.Utilities;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Domain.Interfaces;

namespace StudioTechBI.Application.Services;

public class InsightService : BaseService, IInsightService
{
    private readonly IInsightEngineClient _insightEngineClient;
    private readonly IModelRepository _modelRepository;
    private readonly IDatasetRepository _datasetRepository;
    private readonly IRepository<InsightJob> _jobRepository;
    private readonly IRepository<Client> _clientRepository;
    private readonly IBlobStorageService _blobStorage;
    private readonly IOptions<InsightEngineOptions> _options;
    private readonly InsightSelectionPipeline _selectionPipeline;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InsightService> _logger;

    public InsightService(
        IUnitOfWork unitOfWork,
        IInsightEngineClient insightEngineClient,
        IModelRepository modelRepository,
        IDatasetRepository datasetRepository,
        IRepository<InsightJob> jobRepository,
        IRepository<Client> clientRepository,
        IBlobStorageService blobStorage,
        IOptions<InsightEngineOptions> options,
        InsightSelectionPipeline selectionPipeline,
        IServiceScopeFactory scopeFactory,
        ILogger<InsightService> logger)
        : base(unitOfWork)
    {
        _insightEngineClient = insightEngineClient;
        _modelRepository = modelRepository;
        _datasetRepository = datasetRepository;
        _jobRepository = jobRepository;
        _clientRepository = clientRepository;
        _blobStorage = blobStorage;
        _options = options;
        _selectionPipeline = selectionPipeline;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ModelDto>> GenerateModelsAsync(Guid clientId, string? blobPathOverride, CancellationToken cancellationToken = default)
    {
        EnsureEnabled();

        var client = await _clientRepository.GetByIdAsync(clientId, cancellationToken)
            ?? throw new InvalidOperationException($"Client {clientId} was not found.");

        var folder = (client.BlobFolderPath ?? client.ClientCode ?? client.Id.ToString()).Trim();
        var blobPath = (blobPathOverride ?? "").Trim();
        if (string.IsNullOrEmpty(blobPath))
        {
            var prefix = $"{folder}/accounting/created/";
            blobPath = await _blobStorage.GetLatestBlobPathByPrefixAsync(prefix, ".xlsx", cancellationToken)
                ?? await _blobStorage.GetLatestBlobPathByPrefixAsync(prefix, ".csv", cancellationToken)
                ?? throw new InvalidOperationException(
                    $"No .xlsx or .csv found under blob prefix '{prefix}'. Place a file in accounting/created or pass BlobPath.");
        }

        await using var blobStream = await _blobStorage.DownloadBlobAsync(blobPath, cancellationToken)
            ?? throw new InvalidOperationException($"Blob not found: {blobPath}");

        string? schemaHash = null;
        List<string>? schemaColumns = null;
        List<Dictionary<string, string>>? previewRows = null;

        if (blobPath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            await using var ms = new MemoryStream();
            await blobStream.CopyToAsync(ms, cancellationToken);
            ms.Position = 0;
            var (cols, rows) = await CsvSampleExtractor.ExtractAsync(ms, CsvSampleExtractor.DefaultMaxRows, cancellationToken);
            schemaHash = SchemaHashHelper.ComputeSchemaHash(cols);
            schemaColumns = cols;
            previewRows = rows;
        }

        _logger.LogInformation(
            "InsightEngine generate for client {ClientId}: blob {BlobPath}, schemaHash={HasHash}, previewRows={PreviewCount}.",
            clientId,
            blobPath,
            schemaHash != null,
            previewRows?.Count ?? 0);

        var request = new GenerateModelRequest
        {
            BlobPath = blobPath,
            ClientId = clientId,
            SchemaHash = schemaHash,
            SchemaColumns = schemaColumns,
            PreviewRows = previewRows
        };

        var models = await _insightEngineClient.GenerateModelsAsync(request, cancellationToken);
        _logger.LogInformation(
            "InsightEngine returned {Count} model candidate(s) for client {ClientId}.",
            models.Count,
            clientId);

        foreach (var dto in models)
        {
            var validatedPath = $"{folder}/accounting/validated/{dto.Id:D}/data.xlsx";
            var existing = await _modelRepository.GetByIdAsync(dto.Id, cancellationToken);
            if (existing == null)
            {
                var entity = new InsightModel
                {
                    Id = dto.Id,
                    ClientId = clientId,
                    MappingJson = dto.MappingJson,
                    ExcelSchemaJson = dto.ExcelSchemaJson,
                    TemplateId = dto.TemplateId?.Trim(),
                    Confidence = dto.ResolveConfidence(),
                    Status = InsightWorkflowStatuses.ReadyForSelection,
                    SchemaHash = schemaHash ?? dto.SchemaHash,
                    ValidatedBlobPath = validatedPath,
                    IsFallback = dto.IsFallback ?? false
                };
                await _modelRepository.AddAsync(entity, cancellationToken);
            }
            else
            {
                existing.MappingJson = dto.MappingJson ?? existing.MappingJson;
                existing.ExcelSchemaJson = dto.ExcelSchemaJson ?? existing.ExcelSchemaJson;
                if (!string.IsNullOrWhiteSpace(dto.TemplateId))
                    existing.TemplateId = dto.TemplateId.Trim();
                existing.Confidence = dto.ResolveConfidence();
                existing.Status = InsightWorkflowStatuses.ReadyForSelection;
                if (schemaHash != null)
                    existing.SchemaHash = schemaHash;
                else if (!string.IsNullOrWhiteSpace(dto.SchemaHash))
                    existing.SchemaHash = dto.SchemaHash;
                existing.ValidatedBlobPath = validatedPath;
                if (dto.IsFallback.HasValue)
                    existing.IsFallback = dto.IsFallback.Value;
                await _modelRepository.UpdateAsync(existing, cancellationToken);
            }
        }

        await UnitOfWork.SaveChangesAsync(cancellationToken);
        return models;
    }

    public async Task<SelectModelResponseDto> SelectModelAsync(Guid modelId, bool queueAsync, CancellationToken cancellationToken = default)
    {
        EnsureEnabled();

        var model = await _modelRepository.GetByIdAsync(modelId, cancellationToken)
            ?? throw new InvalidOperationException($"Model {modelId} was not found.");

        var existingActive = await _datasetRepository.GetActiveByModelIdAsync(modelId, cancellationToken);
        if (existingActive != null)
        {
            _logger.LogInformation("Select model {ModelId}: idempotent return (active dataset already exists).", modelId);
            return new SelectModelResponseDto
            {
                DatasetId = existingActive.PowerBIDatasetId,
                ReportId = existingActive.ReportId,
                Queued = false,
                JobId = null,
                Message = "Report created successfully"
            };
        }

        var client = await _clientRepository.GetByIdAsync(model.ClientId, cancellationToken)
            ?? throw new InvalidOperationException($"Client {model.ClientId} was not found.");

        var folder = (client.BlobFolderPath ?? client.ClientCode ?? client.Id.ToString()).Trim();
        var validatedDataBlobPath = $"{folder}/accounting/validated/{modelId:D}/data.xlsx";
        model.ValidatedBlobPath = validatedDataBlobPath;
        model.Status = InsightWorkflowStatuses.Processing;
        await _modelRepository.UpdateAsync(model, cancellationToken);

        if (queueAsync)
        {
            var job = new InsightJob
            {
                Id = Guid.NewGuid(),
                ModelId = modelId,
                Status = InsightJobStatuses.Queued,
                StartedAt = DateTime.UtcNow
            };
            await _jobRepository.AddAsync(job, cancellationToken);
            await UnitOfWork.SaveChangesAsync(cancellationToken);

            ScheduleBackgroundSelect(modelId, job.Id);

            return new SelectModelResponseDto
            {
                Queued = true,
                JobId = job.Id,
                Message = "Processing started",
                DatasetId = null,
                ReportId = null
            };
        }

        var syncJob = new InsightJob
        {
            Id = Guid.NewGuid(),
            ModelId = modelId,
            Status = InsightJobStatuses.Processing,
            StartedAt = DateTime.UtcNow
        };
        await _jobRepository.AddAsync(syncJob, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        var result = await _selectionPipeline.ExecuteAsync(modelId, syncJob.Id, cancellationToken);
        return MapSelectResponse(result);
    }

    private void ScheduleBackgroundSelect(Guid modelId, Guid jobId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var pipeline = scope.ServiceProvider.GetRequiredService<InsightSelectionPipeline>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<InsightService>>();
                var result = await pipeline.ExecuteAsync(modelId, jobId, CancellationToken.None);
                logger.LogInformation(
                    "Background model select finished for {ModelId} job {JobId}: Success={Success}",
                    modelId,
                    jobId,
                    result.Success);
            }
            catch (Exception ex)
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<InsightService>>();
                    logger.LogError(ex, "Background model select failed for {ModelId} job {JobId}.", modelId, jobId);
                }
                catch
                {
                    // ignored
                }
            }
        });
    }

    private static SelectModelResponseDto MapSelectResponse(OrchestratorResultDto result)
    {
        if (result.Success
            && !string.IsNullOrWhiteSpace(result.PowerBIDatasetId)
            && !string.IsNullOrWhiteSpace(result.ReportId))
        {
            return new SelectModelResponseDto
            {
                DatasetId = result.PowerBIDatasetId.Trim(),
                ReportId = result.ReportId.Trim(),
                Queued = false,
                JobId = null,
                Message = "Report created successfully"
            };
        }

        return new SelectModelResponseDto
        {
            DatasetId = null,
            ReportId = null,
            Queued = false,
            JobId = null,
            Message = result.Message ?? "Orchestration failed"
        };
    }

    public async Task<IReadOnlyList<ModelDto>> GetModelsForClientAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        var entities = await _modelRepository.GetByClientIdAsync(clientId, cancellationToken);
        var ordered = entities.OrderByDescending(m => m.CreatedAt).ToList();
        var lookup = await _datasetRepository.GetActiveDatasetsByModelIdsAsync(ordered.Select(m => m.Id), cancellationToken);

        return ordered
            .Select(m =>
            {
                lookup.TryGetValue(m.Id, out var ds);
                return new ModelDto
                {
                    Id = m.Id,
                    TemplateId = m.TemplateId,
                    Status = m.Status,
                    Confidence = m.Confidence,
                    MappingJson = m.MappingJson,
                    ExcelSchemaJson = m.ExcelSchemaJson,
                    SchemaHash = m.SchemaHash,
                    ValidatedBlobPath = m.ValidatedBlobPath,
                    IsFallback = m.IsFallback,
                    DatasetId = ds?.PowerBIDatasetId,
                    ReportId = ds?.ReportId
                };
            })
            .ToList();
    }

    private void EnsureEnabled()
    {
        if (!_options.Value.Enabled)
            throw new InvalidOperationException("Insight Engine integration is disabled (InsightEngine:Enabled=false).");
    }
}
