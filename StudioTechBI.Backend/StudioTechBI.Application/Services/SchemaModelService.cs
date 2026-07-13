using StudioTechBI.Application.DTOs.SchemaModels;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Domain.Interfaces;

namespace StudioTechBI.Application.Services;

public class SchemaModelService : ISchemaModelService
{
    private readonly IRepository<SchemaModel> _modelRepository;
    private readonly IRepository<SchemaModelField> _fieldRepository;
    private readonly IRepository<Template> _templateRepository;

    public SchemaModelService(
        IRepository<SchemaModel> modelRepository,
        IRepository<SchemaModelField> fieldRepository,
        IRepository<Template> templateRepository)
    {
        _modelRepository = modelRepository;
        _fieldRepository = fieldRepository;
        _templateRepository = templateRepository;
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

    private static SchemaModelDto MapToDto(SchemaModel model, IReadOnlyList<SchemaModelField> fields, List<Guid> templateIds) =>
        new(
            model.Id,
            model.Name,
            model.Industry,
            model.Description,
            fields.Select(f => new SchemaModelFieldDto(f.FieldName, f.DataType, f.IsRequired, f.SortOrder)).ToList(),
            templateIds);
}
