using Microsoft.Extensions.Logging;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Domain.Interfaces;

namespace StudioTechBI.Application.Services;

public class ReportDataUsageConsentService : BaseService, IReportDataUsageConsentService
{
    private readonly IRepository<ReportMatchDraft> _draftRepository;
    private readonly IRepository<ReportDataUsageConsent> _consentRepository;
    private readonly ILogger<ReportDataUsageConsentService> _logger;

    public ReportDataUsageConsentService(
        IUnitOfWork unitOfWork,
        IRepository<ReportMatchDraft> draftRepository,
        IRepository<ReportDataUsageConsent> consentRepository,
        ILogger<ReportDataUsageConsentService> logger)
        : base(unitOfWork)
    {
        _draftRepository = draftRepository;
        _consentRepository = consentRepository;
        _logger = logger;
    }

    public async Task<DateTimeOffset> RecordConsentAsync(Guid draftId, Guid clientId, string approvedBy, CancellationToken cancellationToken = default)
    {
        var draft = await _draftRepository.GetByIdAsync(draftId, cancellationToken);
        if (draft is null || draft.ClientId != clientId)
            throw new InvalidOperationException("Report match draft not found for this client.");

        var approvedAt = DateTime.UtcNow;

        // Deliberately no upsert-once check — append-only, one row per confirmation
        // (and later, one per Publish), unlike Consent #1's single-row-per-schema pattern.
        await _consentRepository.AddAsync(new ReportDataUsageConsent
        {
            Id = Guid.NewGuid(),
            ReportMatchDraftId = draftId,
            ClientId = clientId,
            ApprovedAt = approvedAt,
            ApprovedBy = approvedBy,
        }, cancellationToken);

        await UnitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "ReportDesigner.DataUsageConsentRecorded DraftId={DraftId} ClientId={ClientId} ApprovedBy={ApprovedBy}",
            draftId, clientId, approvedBy);

        return approvedAt;
    }
}
