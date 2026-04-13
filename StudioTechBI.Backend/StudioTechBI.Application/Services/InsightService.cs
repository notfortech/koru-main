using System.Net.Http;
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

    public async Task<OrchestratorResultDto> SelectModelAsync(Guid modelId, CancellationToken cancellationToken = default)
    {
        EnsureEnabled();

        var model = await _modelRepository.GetByIdAsync(modelId, cancellationToken)
            ?? throw new InvalidOperationException($"Model {modelId} was not found.");

        var existingActive = await _datasetRepository.GetActiveByModelIdAsync(modelId, cancellationToken);
        if (existingActive != null)
        {
            _logger.LogInformation("Select model {ModelId}: idempotent return (active dataset already exists).", modelId);
            return new OrchestratorResultDto
            {
                Success = true,
                PowerBIDatasetId = existingActive.PowerBIDatasetId,
                ReportId = existingActive.ReportId,
                Message = "Already completed."
            };
        }

        var client = await _clientRepository.GetByIdAsync(model.ClientId, cancellationToken)
            ?? throw new InvalidOperationException($"Client {model.ClientId} was not found.");

        var folder = (client.BlobFolderPath ?? client.ClientCode ?? client.Id.ToString()).Trim();
        var validatedDataBlobPath = $"{folder}/accounting/validated/{modelId:D}/data.xlsx";
        model.ValidatedBlobPath = validatedDataBlobPath;

        var job = new InsightJob
        {
            Id = Guid.NewGuid(),
            ModelId = modelId,
            Status = InsightJobStatuses.Processing,
            StartedAt = DateTime.UtcNow
        };
        await _jobRepository.AddAsync(job, cancellationToken);
        model.Status = InsightWorkflowStatuses.Processing;
        await _modelRepository.UpdateAsync(model, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        OrchestratorResultDto result;
        try
        {
            result = await _insightEngineClient.SelectModelAsync(modelId, validatedDataBlobPath, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "InsightEngine HTTP failure during select for model {ModelId}.", modelId);
            await FailJobAndModelAsync(job, model, ex.Message ?? "HTTP error", cancellationToken);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InsightEngine select failed for model {ModelId}.", modelId);
            await FailJobAndModelAsync(job, model, ex.Message, cancellationToken);
            throw;
        }

        _logger.LogInformation(
            "InsightEngine orchestrator for model {ModelId}: Success={Success}, DatasetId={DatasetId}, ReportId={ReportId}.",
            modelId,
            result.Success,
            result.PowerBIDatasetId,
            result.ReportId);

        job.CompletedAt = DateTime.UtcNow;

        if (result.Success
            && !string.IsNullOrWhiteSpace(result.PowerBIDatasetId)
            && !string.IsNullOrWhiteSpace(result.ReportId))
        {
            job.Status = InsightJobStatuses.Completed;
            job.ErrorMessage = null;
            model.Status = InsightWorkflowStatuses.Active;
            await _jobRepository.UpdateAsync(job, cancellationToken);
            await _modelRepository.UpdateAsync(model, cancellationToken);

            var dataset = await _datasetRepository.GetLatestByModelIdAsync(modelId, cancellationToken);
            if (dataset != null)
            {
                dataset.PowerBIDatasetId = result.PowerBIDatasetId.Trim();
                dataset.ReportId = result.ReportId.Trim();
                dataset.Status = InsightDatasetStatuses.Active;
                await _datasetRepository.UpdateAsync(dataset, cancellationToken);
            }
            else
            {
                await _datasetRepository.AddAsync(new InsightDataset
                {
                    Id = Guid.NewGuid(),
                    ModelId = modelId,
                    PowerBIDatasetId = result.PowerBIDatasetId.Trim(),
                    ReportId = result.ReportId.Trim(),
                    Status = InsightDatasetStatuses.Active
                }, cancellationToken);
            }

            await UnitOfWork.SaveChangesAsync(cancellationToken);
        }
        else
        {
            job.Status = InsightJobStatuses.Failed;
            job.ErrorMessage = result.Message ?? "Orchestrator did not return dataset/report identifiers.";
            model.Status = InsightWorkflowStatuses.Failed;
            await _jobRepository.UpdateAsync(job, cancellationToken);
            await _modelRepository.UpdateAsync(model, cancellationToken);

            var latest = await _datasetRepository.GetLatestByModelIdAsync(modelId, cancellationToken);
            if (latest != null)
            {
                latest.Status = InsightDatasetStatuses.Failed;
                await _datasetRepository.UpdateAsync(latest, cancellationToken);
            }

            await UnitOfWork.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    private async Task FailJobAndModelAsync(InsightJob job, InsightModel model, string error, CancellationToken cancellationToken)
    {
        job.Status = InsightJobStatuses.Failed;
        job.CompletedAt = DateTime.UtcNow;
        job.ErrorMessage = error;
        model.Status = InsightWorkflowStatuses.Failed;
        await _jobRepository.UpdateAsync(job, cancellationToken);
        await _modelRepository.UpdateAsync(model, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ModelDto>> GetModelsForClientAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        var entities = await _modelRepository.GetByClientIdAsync(clientId, cancellationToken);
        return entities
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new ModelDto
            {
                Id = m.Id,
                TemplateId = m.TemplateId,
                Status = m.Status,
                Confidence = m.Confidence,
                MappingJson = m.MappingJson,
                ExcelSchemaJson = m.ExcelSchemaJson,
                SchemaHash = m.SchemaHash,
                ValidatedBlobPath = m.ValidatedBlobPath,
                IsFallback = m.IsFallback
            })
            .ToList();
    }

    private void EnsureEnabled()
    {
        if (!_options.Value.Enabled)
            throw new InvalidOperationException("Insight Engine integration is disabled (InsightEngine:Enabled=false).");
    }
}
