using System.Text;
using StudioTechBI.Application.DTOs.ReportDesigner;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Domain.Interfaces;

namespace StudioTechBI.Application.Services;

public class ReportMatchService : BaseService, IReportMatchService
{
    private readonly IRepository<SchemaModel> _modelRepository;
    private readonly IRepository<SchemaModelField> _fieldRepository;
    private readonly IRepository<Template> _templateRepository;
    private readonly IRepository<ReportMatchDraft> _draftRepository;
    private readonly IRepository<ReportMatchColumnMapping> _mappingRepository;

    public ReportMatchService(
        IUnitOfWork unitOfWork,
        IRepository<SchemaModel> modelRepository,
        IRepository<SchemaModelField> fieldRepository,
        IRepository<Template> templateRepository,
        IRepository<ReportMatchDraft> draftRepository,
        IRepository<ReportMatchColumnMapping> mappingRepository)
        : base(unitOfWork)
    {
        _modelRepository = modelRepository;
        _fieldRepository = fieldRepository;
        _templateRepository = templateRepository;
        _draftRepository = draftRepository;
        _mappingRepository = mappingRepository;
    }

    public async Task<ReportMatchResultDto> MatchAsync(Guid clientId, ExtractedSchemaDto schema, CancellationToken cancellationToken = default)
    {
        var clientColumns = schema.Tables
            .SelectMany(t => t.Columns)
            .GroupBy(c => NormalizeKey(c.ColumnName))
            .Select(g => g.First())
            .ToList();

        var approvedModels = (await _modelRepository.FindAsync(m => m.ReviewStatus == "Approved", cancellationToken)).ToList();

        SchemaModel? bestModel = null;
        var bestScore = 0.0;
        IReadOnlyList<SchemaModelField> bestFields = Array.Empty<SchemaModelField>();

        foreach (var model in approvedModels)
        {
            var fields = (await _fieldRepository.FindAsync(f => f.SchemaModelId == model.Id, cancellationToken))
                .OrderBy(f => f.SortOrder)
                .ToList();
            if (fields.Count == 0)
                continue;

            var score = ScoreModel(fields, clientColumns);
            if (score > bestScore)
            {
                bestScore = score;
                bestModel = model;
                bestFields = fields;
            }
        }

        var mappingDtos = new List<ReportMatchColumnMappingDto>();
        var mappingEntities = new List<ReportMatchColumnMapping>();

        foreach (var field in bestFields)
        {
            var match = clientColumns.FirstOrDefault(c => NormalizeKey(c.ColumnName) == NormalizeKey(field.FieldName));
            var included = match is not null;

            mappingDtos.Add(new ReportMatchColumnMappingDto(field.FieldName, field.DataType, field.IsRequired, match?.ColumnName, included));
            mappingEntities.Add(new ReportMatchColumnMapping
            {
                Id = Guid.NewGuid(),
                FieldName = field.FieldName,
                DataType = field.DataType,
                ClientColumnName = match?.ColumnName,
                Included = included,
            });
        }

        var candidates = new List<ReportMatchCandidateTemplateDto>();
        if (bestModel is not null)
        {
            var templates = await _templateRepository.FindAsync(t => t.SchemaModelId == bestModel.Id, cancellationToken);
            candidates = templates.Select(t => new ReportMatchCandidateTemplateDto(t.Id, t.TemplateName, t.IsPublishReady)).ToList();
        }

        // Upsert: reuse an existing Draft-status draft for the same client+schema on re-match,
        // rather than piling up duplicates every time the user revisits this step.
        var existingDraft = await _draftRepository.FirstOrDefaultAsync(
            d => d.ClientId == clientId && d.SchemaHash == schema.SchemaHash && d.Status == "Draft",
            cancellationToken);

        ReportMatchDraft draft;
        if (existingDraft is not null)
        {
            draft = existingDraft;
            draft.SchemaModelId = bestModel?.Id;
            draft.TemplateId = candidates.Count == 1 ? candidates[0].TemplateId : null;
            await _draftRepository.UpdateAsync(draft, cancellationToken);

            var oldMappings = await _mappingRepository.FindAsync(m => m.ReportMatchDraftId == draft.Id, cancellationToken);
            await _mappingRepository.DeleteRangeAsync(oldMappings, cancellationToken);
        }
        else
        {
            draft = new ReportMatchDraft
            {
                Id = Guid.NewGuid(),
                ClientId = clientId,
                SchemaModelId = bestModel?.Id,
                TemplateId = candidates.Count == 1 ? candidates[0].TemplateId : null,
                SchemaHash = schema.SchemaHash,
                Status = "Draft",
            };
            await _draftRepository.AddAsync(draft, cancellationToken);
        }

        foreach (var mapping in mappingEntities)
            mapping.ReportMatchDraftId = draft.Id;
        await _mappingRepository.AddRangeAsync(mappingEntities, cancellationToken);

        await UnitOfWork.SaveChangesAsync(cancellationToken);

        return new ReportMatchResultDto(
            draft.Id,
            bestModel?.Id,
            bestModel?.Name,
            bestModel?.Industry,
            Math.Round(bestScore, 4),
            candidates,
            mappingDtos);
    }

    private static double ScoreModel(IReadOnlyList<SchemaModelField> fields, IReadOnlyList<ColumnSchemaDto> clientColumns)
    {
        var clientSet = new HashSet<string>(clientColumns.Select(c => NormalizeKey(c.ColumnName)));
        var required = fields.Where(f => f.IsRequired).ToList();
        var optional = fields.Where(f => !f.IsRequired).ToList();

        var requiredHits = required.Count(f => clientSet.Contains(NormalizeKey(f.FieldName)));
        var optionalHits = optional.Count(f => clientSet.Contains(NormalizeKey(f.FieldName)));

        var requiredRatio = required.Count == 0 ? 0 : (double)requiredHits / required.Count;
        var optionalRatio = optional.Count == 0 ? 0 : (double)optionalHits / optional.Count;

        // Mirrors TemplateMatchingService.ScoreTemplate's weighting: required overlap dominates.
        return 0.8 * requiredRatio + 0.2 * optionalRatio;
    }

    private static string NormalizeKey(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return string.Empty;

        var sb = new StringBuilder(s.Length);
        foreach (var c in s.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
        }
        return sb.ToString();
    }
}
