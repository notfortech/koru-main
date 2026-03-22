namespace StudioTechBI.Application.Interfaces;

/// <summary>Writes technical log entries to reporting.TechnicalLogs.</summary>
public interface IReportingTechnicalLogWriter
{
    Task LogAsync(string service, string level, string message, string? stackTrace = null, CancellationToken cancellationToken = default);
}
