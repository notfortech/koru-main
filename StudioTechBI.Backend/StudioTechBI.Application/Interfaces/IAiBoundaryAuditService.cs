using StudioTechBI.Application.DTOs.Admin;

namespace StudioTechBI.Application.Interfaces;

/// <summary>
/// Durable, queryable sink for every AI-boundary crossing (see AiBoundaryAuditEvent). Every
/// call site that talks to an AI-backed service should log a "Sent" event immediately before
/// the call and a "Received" (or "Failed") event immediately after — mirroring the existing
/// ReportDesigner.SchemaSentToAI / AIResponseReceived structured-log pair, but written to
/// durable storage instead of (or as well as) the application log.
/// </summary>
public interface IAiBoundaryAuditService
{
    Task LogSentAsync(
        string service,
        string operation,
        string correlationId,
        string metadataJson,
        Guid? clientId = null,
        CancellationToken cancellationToken = default);

    Task LogReceivedAsync(
        string service,
        string operation,
        string correlationId,
        string metadataJson,
        long durationMs,
        int? statusCode = null,
        Guid? clientId = null,
        CancellationToken cancellationToken = default);

    Task LogFailedAsync(
        string service,
        string operation,
        string correlationId,
        long durationMs,
        string errorSummary,
        int? statusCode = null,
        Guid? clientId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiBoundaryAuditEventDto>> GetRecentAsync(
        int? limit,
        string? correlationId,
        string? operation,
        bool? success,
        CancellationToken cancellationToken = default);
}
