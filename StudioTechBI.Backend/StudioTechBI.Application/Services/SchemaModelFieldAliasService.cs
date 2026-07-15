using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.SchemaModels;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Domain.Interfaces;

namespace StudioTechBI.Application.Services;

public class SchemaModelFieldAliasService : BaseService, ISchemaModelFieldAliasService
{
    private readonly IRepository<SchemaModelFieldAlias> _aliasRepository;
    private readonly IRepository<SchemaModelField> _fieldRepository;
    private readonly IRepository<SchemaModel> _modelRepository;
    private readonly ILogger<SchemaModelFieldAliasService> _logger;

    public SchemaModelFieldAliasService(
        IUnitOfWork unitOfWork,
        IRepository<SchemaModelFieldAlias> aliasRepository,
        IRepository<SchemaModelField> fieldRepository,
        IRepository<SchemaModel> modelRepository,
        ILogger<SchemaModelFieldAliasService> logger)
        : base(unitOfWork)
    {
        _aliasRepository = aliasRepository;
        _fieldRepository = fieldRepository;
        _modelRepository = modelRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SchemaModelFieldAliasDto>> GetPendingReviewAsync(CancellationToken cancellationToken = default)
    {
        var aliases = (await _aliasRepository.FindAsync(a => a.ApprovalStatus == "PendingReview", cancellationToken))
            .OrderByDescending(a => a.ObservedCount)
            .ThenByDescending(a => a.LastSeenAt)
            .ToList();

        return await MapToDtosAsync(aliases, cancellationToken);
    }

    public Task<SchemaModelFieldAliasDto?> ApproveAsync(Guid id, string approvedBy, CancellationToken cancellationToken = default) =>
        SetApprovalStatusAsync(id, "Approved", approvedBy, cancellationToken);

    public Task<SchemaModelFieldAliasDto?> RejectAsync(Guid id, string rejectedBy, CancellationToken cancellationToken = default) =>
        SetApprovalStatusAsync(id, "Rejected", rejectedBy, cancellationToken);

    private async Task<SchemaModelFieldAliasDto?> SetApprovalStatusAsync(Guid id, string status, string decidedBy, CancellationToken cancellationToken)
    {
        var alias = await _aliasRepository.GetByIdAsync(id, cancellationToken);
        if (alias is null || alias.ApprovalStatus != "PendingReview")
            return null;

        alias.ApprovalStatus = status;
        alias.DecidedBy = decidedBy;
        alias.DecidedAt = DateTime.UtcNow;
        await _aliasRepository.UpdateAsync(alias, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "SchemaModelFieldAlias.ReviewDecision AliasId={AliasId} SchemaModelFieldId={SchemaModelFieldId} Status={Status} DecidedBy={DecidedBy}",
            id, alias.SchemaModelFieldId, status, decidedBy);

        var dtos = await MapToDtosAsync(new List<SchemaModelFieldAlias> { alias }, cancellationToken);
        return dtos.FirstOrDefault();
    }

    private async Task<IReadOnlyList<SchemaModelFieldAliasDto>> MapToDtosAsync(
        IReadOnlyList<SchemaModelFieldAlias> aliases, CancellationToken cancellationToken)
    {
        if (aliases.Count == 0)
            return Array.Empty<SchemaModelFieldAliasDto>();

        var fieldIds = aliases.Select(a => a.SchemaModelFieldId).Distinct().ToList();
        var fields = (await _fieldRepository.FindAsync(f => fieldIds.Contains(f.Id), cancellationToken))
            .ToDictionary(f => f.Id);

        var modelIds = fields.Values.Select(f => f.SchemaModelId).Distinct().ToList();
        var models = (await _modelRepository.FindAsync(m => modelIds.Contains(m.Id), cancellationToken))
            .ToDictionary(m => m.Id);

        return aliases
            .Select(a =>
            {
                fields.TryGetValue(a.SchemaModelFieldId, out var field);
                var model = field is not null && models.TryGetValue(field.SchemaModelId, out var m) ? m : null;

                return new SchemaModelFieldAliasDto(
                    a.Id,
                    a.SchemaModelFieldId,
                    field?.FieldName ?? "(unknown field)",
                    field?.SchemaModelId ?? Guid.Empty,
                    model?.Name ?? "(unknown model)",
                    model?.Industry ?? "",
                    a.AliasName,
                    a.ObservedDataType,
                    a.Confidence,
                    a.Source,
                    a.ApprovalStatus,
                    a.ObservedCount,
                    a.FirstSeenAt,
                    a.LastSeenAt,
                    a.DecidedBy,
                    a.DecidedAt);
            })
            .ToList();
    }
}
