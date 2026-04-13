using Microsoft.Extensions.Options;
using StudioTechBI.Application.DTOs.Insight;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;
using StudioTechBI.Domain.Interfaces;

namespace StudioTechBI.Application.Services;

public class InsightReportService : IInsightReportService
{
    private readonly IModelRepository _modelRepository;
    private readonly IDatasetRepository _datasetRepository;
    private readonly PowerBIService _powerBiService;
    private readonly PowerBISettings _powerBiSettings;

    public InsightReportService(
        IModelRepository modelRepository,
        IDatasetRepository datasetRepository,
        PowerBIService powerBiService,
        IOptions<PowerBISettings> powerBiSettings)
    {
        _modelRepository = modelRepository;
        _datasetRepository = datasetRepository;
        _powerBiService = powerBiService;
        _powerBiSettings = powerBiSettings.Value;
    }

    public async Task<InsightReportEmbedDto?> GetInsightReportEmbedAsync(Guid modelId, CancellationToken cancellationToken = default)
    {
        var model = await _modelRepository.GetByIdAsync(modelId, cancellationToken);
        if (model == null)
            return null;

        var ds = await _datasetRepository.GetActiveByModelIdAsync(modelId, cancellationToken);
        if (ds == null)
            throw new InvalidOperationException("No active Power BI dataset for this model. Complete model selection first.");

        var workspaceId = (_powerBiSettings.WorkspaceId ?? "").Trim();
        if (workspaceId.Length == 0)
            throw new InvalidOperationException("PowerBI:WorkspaceId is not configured.");

        var embed = await _powerBiService.GenerateEmbedForReportAsync(
            workspaceId,
            ds.ReportId,
            ds.PowerBIDatasetId,
            cancellationToken);

        return new InsightReportEmbedDto
        {
            DatasetId = ds.PowerBIDatasetId,
            ReportId = ds.ReportId,
            EmbedUrl = embed.EmbedUrl,
            AccessToken = embed.AccessToken
        };
    }
}
