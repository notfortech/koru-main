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
}
