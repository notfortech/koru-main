using Microsoft.Extensions.Logging;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Domain.Interfaces;

namespace StudioTechBI.Application.Services;

public class ReportDesignerConsentService : BaseService, IReportDesignerConsentService
{
    private readonly IRepository<ReportDesignerConsent> _consentRepository;
    private readonly ILogger<ReportDesignerConsentService> _logger;

    public ReportDesignerConsentService(
        IUnitOfWork unitOfWork,
        IRepository<ReportDesignerConsent> consentRepository,
        ILogger<ReportDesignerConsentService> logger)
        : base(unitOfWork)
    {
        _consentRepository = consentRepository;
        _logger = logger;
    }

    public async Task<bool> HasConsentAsync(Guid clientId, string schemaHash, CancellationToken cancellationToken = default)
    {
        return await _consentRepository.AnyAsync(
            c => !c.IsDeleted && c.ClientId == clientId && c.SchemaHash == schemaHash,
            cancellationToken);
    }

    public async Task GrantConsentAsync(Guid clientId, string schemaHash, string approvedBy, CancellationToken cancellationToken = default)
    {
        var existing = await _consentRepository.FirstOrDefaultAsync(
            c => !c.IsDeleted && c.ClientId == clientId && c.SchemaHash == schemaHash,
            cancellationToken);

        if (existing != null)
        {
            _logger.LogInformation(
                "ReportDesigner.ConsentAlreadyRecorded ClientId={ClientId} SchemaHash={SchemaHash}",
                clientId, schemaHash);
            return;
        }

        // Store consent BEFORE the AI call is ever made (single event) — mirrors the
        // ModelConsent pattern used for Insights Engine approvals.
        await _consentRepository.AddAsync(new ReportDesignerConsent
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            SchemaHash = schemaHash,
            ApprovedAt = DateTime.UtcNow,
            ApprovedBy = approvedBy
        }, cancellationToken);

        await UnitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "ReportDesigner.ConsentGranted ClientId={ClientId} SchemaHash={SchemaHash} ApprovedBy={ApprovedBy}",
            clientId, schemaHash, approvedBy);
    }
}
