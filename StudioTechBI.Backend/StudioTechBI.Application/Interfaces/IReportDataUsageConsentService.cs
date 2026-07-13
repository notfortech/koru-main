namespace StudioTechBI.Application.Interfaces;

/// <summary>
/// Consent #2 in the AI-Assisted report flow — see ReportDataUsageConsent entity for context.
/// Deliberately append-only (no upsert-once check like Consent #1): every call records a new row.
/// </summary>
public interface IReportDataUsageConsentService
{
    Task<DateTimeOffset> RecordConsentAsync(Guid draftId, Guid clientId, string approvedBy, CancellationToken cancellationToken = default);
}
