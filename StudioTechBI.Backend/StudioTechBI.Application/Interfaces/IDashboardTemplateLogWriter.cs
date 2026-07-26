namespace StudioTechBI.Application.Interfaces;

/// <summary>
/// Persists a Dashboard Template Generator run (success or failure) so it's visible on the admin
/// "Dashboard Template Logs" page. Reuses the existing FunctionalLog entity/table (already has
/// ClientId + Timestamp — exactly what's needed to differentiate repeat generations for the same
/// client) rather than inventing a new log table.
/// </summary>
public interface IDashboardTemplateLogWriter
{
    Task LogAsync(
        Guid? clientId,
        string clientName,
        bool success,
        string summary,
        IReadOnlyList<string> logLines,
        CancellationToken cancellationToken = default);

    /// <summary>No confident, publish-ready catalog match was found for this client's blended
    /// dataset — files a build request for staff (EventType "DashboardTemplateBuildRequested")
    /// pointing at the blob path + requested columns so a designer can act on it.</summary>
    Task LogBuildRequestAsync(
        Guid clientId,
        string clientName,
        string blobPath,
        IReadOnlyList<string> requestedColumns,
        string correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>The template match check (or a matched template's clone) failed technically —
    /// logs the real exception detail for staff (EventType "DashboardTemplateMatchCheckFailed")
    /// while the client only ever sees a generic "contact support" message.</summary>
    Task LogMatchCheckFailedAsync(
        Guid clientId,
        string clientName,
        string correlationId,
        Exception exception,
        CancellationToken cancellationToken = default);
}
