namespace StudioTechBI.Application.Interfaces;

/// <summary>
/// Gates the Report Designer AI matching step behind explicit, auditable client consent.
/// Consent is scoped to a specific schema shape (via SchemaHash), not granted globally.
/// </summary>
public interface IReportDesignerConsentService
{
    /// <summary>True if the client has already granted AI-matching consent for this exact schema shape.</summary>
    Task<bool> HasConsentAsync(Guid clientId, string schemaHash, CancellationToken cancellationToken = default);

    /// <summary>Records consent (idempotent — safe to call more than once for the same schema). Call only after the user has explicitly agreed.</summary>
    Task GrantConsentAsync(Guid clientId, string schemaHash, string approvedBy, CancellationToken cancellationToken = default);
}
