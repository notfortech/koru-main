using System.Net.Http;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.Insight;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Domain.Interfaces;

namespace StudioTechBI.Application.Services;

/// <summary>Runs InsightEngine orchestration for an existing <see cref="InsightJob"/> (sync or background).</summary>
public class InsightSelectionPipeline
{
    private readonly IInsightEngineClient _insightEngineClient;
    private readonly IModelRepository _modelRepository;
    private readonly IDatasetRepository _datasetRepository;
    private readonly IRepository<InsightJob> _jobRepository;
    private readonly IRepository<Client> _clientRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<InsightSelectionPipeline> _logger;

    public InsightSelectionPipeline(
        IInsightEngineClient insightEngineClient,
        IModelRepository modelRepository,
        IDatasetRepository datasetRepository,
        IRepository<InsightJob> jobRepository,
        IRepository<Client> clientRepository,
        IUnitOfWork unitOfWork,
        ILogger<InsightSelectionPipeline> logger)
    {
        _insightEngineClient = insightEngineClient;
        _modelRepository = modelRepository;
        _datasetRepository = datasetRepository;
        _jobRepository = jobRepository;
        _clientRepository = clientRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OrchestratorResultDto> ExecuteAsync(Guid modelId, Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken);
        var model = await _modelRepository.GetByIdAsync(modelId, cancellationToken);
        if (job == null || model == null || job.ModelId != modelId)
        {
            return new OrchestratorResultDto
            {
                Success = false,
                Message = "Invalid model or job for orchestration."
            };
        }

        if (job.Status == InsightJobStatuses.Queued)
        {
            job.Status = InsightJobStatuses.Processing;
            await _jobRepository.UpdateAsync(job, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var validatedDataBlobPath = model.ValidatedBlobPath;
        if (string.IsNullOrWhiteSpace(validatedDataBlobPath))
        {
            var client = await _clientRepository.GetByIdAsync(model.ClientId, cancellationToken);
            if (client == null)
            {
                await FailJobAndModelAsync(job, model, "Client not found.", cancellationToken);
                return new OrchestratorResultDto { Success = false, Message = "Client not found." };
            }

            var folder = (client.BlobFolderPath ?? client.ClientCode ?? client.Id.ToString()).Trim();
            validatedDataBlobPath = $"{folder}/accounting/validated/{modelId:D}/data.xlsx";
            model.ValidatedBlobPath = validatedDataBlobPath;
            await _modelRepository.UpdateAsync(model, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        OrchestratorResultDto result;
        try
        {
            result = await _insightEngineClient.SelectModelAsync(modelId, validatedDataBlobPath, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "InsightEngine HTTP failure during select for model {ModelId}.", modelId);
            await FailJobAndModelAsync(job, model, ex.Message ?? "HTTP error", cancellationToken);
            return new OrchestratorResultDto { Success = false, Message = ex.Message ?? "HTTP error" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InsightEngine select failed for model {ModelId}.", modelId);
            await FailJobAndModelAsync(job, model, ex.Message, cancellationToken);
            return new OrchestratorResultDto { Success = false, Message = ex.Message };
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

            await _unitOfWork.SaveChangesAsync(cancellationToken);
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

            await _unitOfWork.SaveChangesAsync(cancellationToken);
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
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
