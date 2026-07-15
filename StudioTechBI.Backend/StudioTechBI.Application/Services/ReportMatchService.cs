using System.Text;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.ReportDesigner;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Domain.Interfaces;

namespace StudioTechBI.Application.Services;

public class ReportMatchService : BaseService, IReportMatchService
{
    /// <summary>
    /// Deterministic scores at or above this proceed without AI involvement. Below it,
    /// MatchAsync escalates to stbi_transformers for a semantic match/propose call.
    /// Placeholder value — see epic open question on tuning against real customer schemas.
    /// </summary>
    private const double AiEscalationThreshold = 0.6;

    private const string SourceDeterministic = "Deterministic";
    private const string SourceAiMatched = "AiMatched";
    private const string SourceAiProposedNew = "AiProposedNew";

    private readonly IRepository<SchemaModel> _modelRepository;
    private readonly IRepository<SchemaModelField> _fieldRepository;
    private readonly IRepository<Template> _templateRepository;
    private readonly IRepository<ReportMatchDraft> _draftRepository;
    private readonly IRepository<ReportMatchColumnMapping> _mappingRepository;
    private readonly IReportDesignerClient _reportDesignerClient;
    private readonly ILogger<ReportMatchService> _logger;

    public ReportMatchService(
        IUnitOfWork unitOfWork,
        IRepository<SchemaModel> modelRepository,
        IRepository<SchemaModelField> fieldRepository,
        IRepository<Template> templateRepository,
        IRepository<ReportMatchDraft> draftRepository,
        IRepository<ReportMatchColumnMapping> mappingRepository,
        IReportDesignerClient reportDesignerClient,
        ILogger<ReportMatchService> logger)
        : base(unitOfWork)
    {
        _modelRepository = modelRepository;
        _fieldRepository = fieldRepository;
        _templateRepository = templateRepository;
        _draftRepository = draftRepository;
        _mappingRepository = mappingRepository;
        _reportDesignerClient = reportDesignerClient;
        _logger = logger;
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
        var fieldsByModel = new Dictionary<Guid, List<SchemaModelField>>();

        foreach (var model in approvedModels)
        {
            var fields = (await _fieldRepository.FindAsync(f => f.SchemaModelId == model.Id, cancellationToken))
                .OrderBy(f => f.SortOrder)
                .ToList();
            fieldsByModel[model.Id] = fields;
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

        var matchSource = SourceDeterministic;
        var pendingSupportReview = false;
        Template? newlyProposedTemplate = null;

        if (bestScore < AiEscalationThreshold)
        {
            var escalated = await TryEscalateToAiAsync(clientId, approvedModels, fieldsByModel, clientColumns, cancellationToken);
            if (escalated is not null)
            {
                (bestModel, bestFields, bestScore, matchSource, pendingSupportReview, newlyProposedTemplate) = escalated.Value;
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
        if (newlyProposedTemplate is not null)
        {
            // Just created, not yet saved — querying the DB for it here would find nothing.
            candidates = new List<ReportMatchCandidateTemplateDto>
            {
                new(newlyProposedTemplate.Id, newlyProposedTemplate.TemplateName, newlyProposedTemplate.IsPublishReady),
            };
        }
        else if (bestModel is not null)
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
            matchSource,
            pendingSupportReview,
            candidates,
            mappingDtos);
    }

    /// <summary>
    /// Calls stbi_transformers to either semantically match one of the Approved candidates
    /// or propose a brand-new SchemaModel. Never throws — any failure (network, parse, or a
    /// hallucinated candidate id) is logged and the caller falls back to the deterministic
    /// result, since a low-confidence deterministic match is still strictly better than a
    /// hard failure of the whole match request.
    /// </summary>
    private async Task<(SchemaModel? Model, IReadOnlyList<SchemaModelField> Fields, double Score, string Source, bool PendingReview, Template? NewTemplate)?> TryEscalateToAiAsync(
        Guid clientId,
        List<SchemaModel> approvedModels,
        Dictionary<Guid, List<SchemaModelField>> fieldsByModel,
        List<ColumnSchemaDto> clientColumns,
        CancellationToken cancellationToken)
    {
        var candidateModels = approvedModels
            .Where(m => fieldsByModel[m.Id].Count > 0)
            .Select(m => new SchemaModelAiMatchCandidate(
                m.Id.ToString(),
                m.Name,
                m.Industry,
                fieldsByModel[m.Id].Select(f => new SchemaModelAiMatchField(f.FieldName, f.DataType, f.IsRequired)).ToList()))
            .ToList();

        if (candidateModels.Count == 0)
            return null;

        var request = new SchemaModelAiMatchRequest(
            clientColumns.Select(c => new SchemaModelAiMatchColumn(c.ColumnName, c.DataType)).ToList(),
            candidateModels);

        var correlationId = Guid.NewGuid().ToString();

        SchemaModelAiMatchResponse response;
        try
        {
            response = await _reportDesignerClient.MatchSchemaModelAsync(request, correlationId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "ReportMatch.AiEscalationFailed ClientId={ClientId} CorrelationId={CorrelationId} — falling back to deterministic result.",
                clientId, correlationId);
            return null;
        }

        if (response.MatchedModelId is Guid matchedId)
        {
            var matched = approvedModels.FirstOrDefault(m => m.Id == matchedId);
            if (matched is null)
            {
                _logger.LogWarning(
                    "ReportMatch.AiMatchedUnknownModel ClientId={ClientId} MatchedModelId={MatchedModelId} CorrelationId={CorrelationId}",
                    clientId, matchedId, correlationId);
                return null;
            }
            return (matched, fieldsByModel[matched.Id], response.Confidence, SourceAiMatched, false, null);
        }

        if (response.ProposedModel is { } proposed)
        {
            var newModel = new SchemaModel
            {
                Id = Guid.NewGuid(),
                Name = proposed.Name,
                Industry = proposed.Industry,
                Description = "Proposed by AI matching. No manual review step — usable immediately, same as a seeded model, but no real .pbix exists yet (see IsPublishReady on its Template).",
                IsAiSuggested = true,
                ReviewStatus = "Approved",
                SuggestedByClientId = clientId,
            };
            await _modelRepository.AddAsync(newModel, cancellationToken);

            var newFields = proposed.Fields.Select((f, i) => new SchemaModelField
            {
                Id = Guid.NewGuid(),
                SchemaModelId = newModel.Id,
                FieldName = f.FieldName,
                DataType = f.DataType,
                IsRequired = f.IsRequired,
                SortOrder = i,
            }).ToList();
            await _fieldRepository.AddRangeAsync(newFields, cancellationToken);

            // AI proposes a name only, not a layout/design — same placeholder-BlobPath
            // situation as every seeded Template until a real .pbix is built and published.
            var templateName = string.IsNullOrWhiteSpace(proposed.TemplateName)
                ? $"{proposed.Name} — Overview"
                : proposed.TemplateName;
            var newTemplate = new Template
            {
                Id = Guid.NewGuid(),
                TemplateName = templateName,
                Industry = proposed.Industry,
                Version = "1.0",
                SchemaModelId = newModel.Id,
                BlobPath = $"templates/ai-proposed/{newModel.Id}/overview.pbix",
                IsPublishReady = false,
                RequiredColumnsJson = System.Text.Json.JsonSerializer.Serialize(
                    proposed.Fields.Where(f => f.IsRequired).Select(f => f.FieldName)),
                OptionalColumnsJson = System.Text.Json.JsonSerializer.Serialize(
                    proposed.Fields.Where(f => !f.IsRequired).Select(f => f.FieldName)),
            };
            await _templateRepository.AddAsync(newTemplate, cancellationToken);

            // Flush now, in its own SaveChanges — ReportMatchDraft.TemplateId has no EF-mapped
            // relationship to Template (deliberate, see ReportMatchDraft's own comment), so EF
            // can't order a later INSERT/UPDATE against this new Template correctly within a
            // single SaveChanges batch. The real FK constraint at the DB level doesn't care that
            // EF doesn't know about it — a same-batch write from MatchAsync's draft-reuse path
            // (an UPDATE on ReportMatchDrafts.TemplateId) hit exactly this and 500'd in production.
            await UnitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "ReportMatch.AiProposedNewModel ClientId={ClientId} SchemaModelId={SchemaModelId} Name={Name} " +
                "TemplateId={TemplateId} CorrelationId={CorrelationId}",
                clientId, newModel.Id, newModel.Name, newTemplate.Id, correlationId);

            return (newModel, newFields, response.Confidence, SourceAiProposedNew, false, newTemplate);
        }

        return null;
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
