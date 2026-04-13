using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.DTOs.Insight;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Domain.Interfaces;

namespace StudioTechBI.Application.Services;

public class InsightService : BaseService, IInsightService
{
    private readonly IInsightEngineClient _insightEngineClient;
    private readonly IModelRepository _modelRepository;
    private readonly IDatasetRepository _datasetRepository;
    private readonly IRepository<Client> _clientRepository;
    private readonly IBlobStorageService _blobStorage;
    private readonly IOptions<InsightEngineOptions> _options;
    private readonly ILogger<InsightService> _logger;

    public InsightService(
        IUnitOfWork unitOfWork,
        IInsightEngineClient insightEngineClient,
        IModelRepository modelRepository,
        IDatasetRepository datasetRepository,
        IRepository<Client> clientRepository,
        IBlobStorageService blobStorage,
        IOptions<InsightEngineOptions> options,
        ILogger<InsightService> logger)
        : base(unitOfWork)
    {
        _insightEngineClient = insightEngineClient;
        _modelRepository = modelRepository;
        _datasetRepository = datasetRepository;
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
                ?? throw new InvalidOperationException(
                    $"No .xlsx found under blob prefix '{prefix}'. Upload to the accounting/created folder or pass an explicit blob path.");
        }

        _logger.LogInformation(
            "InsightEngine generate models for client {ClientId} using blob path {BlobPath}.",
            clientId,
            blobPath);

        var request = new GenerateModelRequest
        {
            BlobPath = blobPath,
            ClientId = clientId
        };

        var models = await _insightEngineClient.GenerateModelsAsync(request, cancellationToken);
        _logger.LogInformation(
            "InsightEngine returned {Count} model candidate(s) for client {ClientId}.",
            models.Count,
            clientId);

        foreach (var dto in models)
        {
            var existing = await _modelRepository.GetByIdAsync(dto.Id, cancellationToken);
            if (existing == null)
            {
                var entity = new InsightModel
                {
                    Id = dto.Id,
                    ClientId = clientId,
                    MappingJson = dto.MappingJson,
                    ExcelSchemaJson = dto.ExcelSchemaJson,
                    TemplateId = dto.TemplateId,
                    Status = dto.Status ?? "Generated",
                    ConfidenceScore = dto.ConfidenceScore
                };
                await _modelRepository.AddAsync(entity, cancellationToken);
            }
            else
            {
                existing.MappingJson = dto.MappingJson ?? existing.MappingJson;
                existing.ExcelSchemaJson = dto.ExcelSchemaJson ?? existing.ExcelSchemaJson;
                existing.TemplateId = dto.TemplateId ?? existing.TemplateId;
                existing.Status = dto.Status ?? existing.Status;
                existing.ConfidenceScore = dto.ConfidenceScore ?? existing.ConfidenceScore;
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

        var client = await _clientRepository.GetByIdAsync(model.ClientId, cancellationToken)
            ?? throw new InvalidOperationException($"Client {model.ClientId} was not found.");

        var folder = (client.BlobFolderPath ?? client.ClientCode ?? client.Id.ToString()).Trim();
        var validatedDataBlobPath = $"{folder}/accounting/validated/{modelId:D}/data.xlsx";

        _logger.LogInformation(
            "InsightEngine select model {ModelId}; expected orchestrator output blob {BlobPath}.",
            modelId,
            validatedDataBlobPath);

        var result = await _insightEngineClient.SelectModelAsync(modelId, validatedDataBlobPath, cancellationToken);

        _logger.LogInformation(
            "InsightEngine orchestrator for model {ModelId}: Success={Success}, DatasetId={DatasetId}, ReportId={ReportId}.",
            modelId,
            result.Success,
            result.PowerBIDatasetId,
            result.ReportId);

        if (result.Success
            && !string.IsNullOrWhiteSpace(result.PowerBIDatasetId)
            && !string.IsNullOrWhiteSpace(result.ReportId))
        {
            model.Status = "Selected";
            await _modelRepository.UpdateAsync(model, cancellationToken);

            var existingDataset = await _datasetRepository.GetLatestByModelIdAsync(modelId, cancellationToken);
            if (existingDataset != null)
            {
                existingDataset.PowerBIDatasetId = result.PowerBIDatasetId.Trim();
                existingDataset.ReportId = result.ReportId.Trim();
                await _datasetRepository.UpdateAsync(existingDataset, cancellationToken);
            }
            else
            {
                var dataset = new InsightDataset
                {
                    Id = Guid.NewGuid(),
                    ModelId = modelId,
                    PowerBIDatasetId = result.PowerBIDatasetId.Trim(),
                    ReportId = result.ReportId.Trim()
                };
                await _datasetRepository.AddAsync(dataset, cancellationToken);
            }

            await UnitOfWork.SaveChangesAsync(cancellationToken);
        }
        else if (!result.Success)
        {
            model.Status = "OrchestrationFailed";
            await _modelRepository.UpdateAsync(model, cancellationToken);
            await UnitOfWork.SaveChangesAsync(cancellationToken);
        }

        return result;
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
                ConfidenceScore = m.ConfidenceScore,
                MappingJson = m.MappingJson,
                ExcelSchemaJson = m.ExcelSchemaJson
            })
            .ToList();
    }

    private void EnsureEnabled()
    {
        if (!_options.Value.Enabled)
            throw new InvalidOperationException("Insight Engine integration is disabled (InsightEngine:Enabled=false).");
    }
}
