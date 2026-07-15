using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.SchemaModels;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Domain.Interfaces;

namespace StudioTechBI.Application.Services;

public class SchemaModelService : BaseService, ISchemaModelService
{
    private readonly IRepository<SchemaModel> _modelRepository;
    private readonly IRepository<SchemaModelField> _fieldRepository;
    private readonly IRepository<Template> _templateRepository;
    private readonly ILogger<SchemaModelService> _logger;

    public SchemaModelService(
        IUnitOfWork unitOfWork,
        IRepository<SchemaModel> modelRepository,
        IRepository<SchemaModelField> fieldRepository,
        IRepository<Template> templateRepository,
        ILogger<SchemaModelService> logger)
        : base(unitOfWork)
    {
        _modelRepository = modelRepository;
        _fieldRepository = fieldRepository;
        _templateRepository = templateRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SchemaModelDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var models = await _modelRepository.GetAllAsync(cancellationToken);
        var fields = await _fieldRepository.GetAllAsync(cancellationToken);
        var templates = await _templateRepository.GetAllAsync(cancellationToken);

        var fieldsByModel = fields
            .GroupBy(f => f.SchemaModelId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<SchemaModelField>)g.OrderBy(f => f.SortOrder).ToList());
        var templateIdsByModel = templates
            .Where(t => t.SchemaModelId.HasValue)
            .GroupBy(t => t.SchemaModelId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(t => t.Id).ToList());

        return models
            .OrderBy(m => m.Industry).ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .Select(m => MapToDto(
                m,
                fieldsByModel.TryGetValue(m.Id, out var f) ? f : Array.Empty<SchemaModelField>(),
                templateIdsByModel.TryGetValue(m.Id, out var t) ? t : new List<Guid>()))
            .ToList();
    }

    public async Task<SchemaModelDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var model = await _modelRepository.GetByIdAsync(id, cancellationToken);
        if (model is null)
            return null;

        var fields = await _fieldRepository.FindAsync(f => f.SchemaModelId == id, cancellationToken);
        var templates = await _templateRepository.FindAsync(t => t.SchemaModelId == id, cancellationToken);

        return MapToDto(
            model,
            fields.OrderBy(f => f.SortOrder).ToList(),
            templates.Select(t => t.Id).ToList());
    }

    public async Task<IReadOnlyList<SchemaModelDto>> GetPendingReviewAsync(CancellationToken cancellationToken = default)
    {
        var models = await _modelRepository.FindAsync(m => m.ReviewStatus == "PendingReview", cancellationToken);
        var fields = await _fieldRepository.GetAllAsync(cancellationToken);
        var fieldsByModel = fields
            .GroupBy(f => f.SchemaModelId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<SchemaModelField>)g.OrderBy(f => f.SortOrder).ToList());

        return models
            .OrderBy(m => m.Industry).ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .Select(m => MapToDto(
                m,
                fieldsByModel.TryGetValue(m.Id, out var f) ? f : Array.Empty<SchemaModelField>(),
                new List<Guid>()))
            .ToList();
    }

    public Task<SchemaModelDto?> ApproveAsync(Guid id, string approvedBy, CancellationToken cancellationToken = default) =>
        SetReviewStatusAsync(id, "Approved", approvedBy, cancellationToken);

    public Task<SchemaModelDto?> RejectAsync(Guid id, string rejectedBy, CancellationToken cancellationToken = default) =>
        SetReviewStatusAsync(id, "Rejected", rejectedBy, cancellationToken);

    private async Task<SchemaModelDto?> SetReviewStatusAsync(Guid id, string status, string decidedBy, CancellationToken cancellationToken)
    {
        var model = await _modelRepository.GetByIdAsync(id, cancellationToken);
        if (model is null || model.ReviewStatus != "PendingReview")
            return null;

        model.ReviewStatus = status;
        await _modelRepository.UpdateAsync(model, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "SchemaModel.ReviewDecision SchemaModelId={SchemaModelId} Status={Status} DecidedBy={DecidedBy}",
            id, status, decidedBy);

        var fields = await _fieldRepository.FindAsync(f => f.SchemaModelId == id, cancellationToken);
        return MapToDto(model, fields.OrderBy(f => f.SortOrder).ToList(), new List<Guid>());
    }

    private static SchemaModelDto MapToDto(SchemaModel model, IReadOnlyList<SchemaModelField> fields, List<Guid> templateIds) =>
        new(
            model.Id,
            model.Name,
            model.Industry,
            model.Description,
            fields.Select(f => new SchemaModelFieldDto(f.FieldName, f.DataType, f.IsRequired, f.SortOrder)).ToList(),
            templateIds,
            model.IsAiSuggested,
            model.ReviewStatus,
            model.SuggestedByClientId);
}
