using Microsoft.EntityFrameworkCore;
using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.Infrastructure.Services;

public class AiBoundaryAuditService : IAiBoundaryAuditService
{
    // The only AI-backed system koru-main calls out to today. Revisit if/when a second
    // target is added (e.g. once S5 gives stbi_transformers its own outbound-to-Anthropic
    // events, or a future integration calls a different AI-backed service directly).
    private const string DefaultTargetService = "stbi_transformers";

    private readonly ApplicationDbContext _db;

    public AiBoundaryAuditService(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task LogSentAsync(
        string service,
        string operation,
        string correlationId,
        string metadataJson,
        Guid? clientId = null,
        CancellationToken cancellationToken = default) =>
        AddAsync(new AiBoundaryAuditEvent
        {
            Id = Guid.NewGuid(),
            CorrelationId = correlationId,
            Service = service,
            Operation = operation,
            Phase = "Sent",
            TargetService = DefaultTargetService,
            MetadataJson = metadataJson,
            ClientId = clientId,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

    public Task LogReceivedAsync(
        string service,
        string operation,
        string correlationId,
        string metadataJson,
        long durationMs,
        int? statusCode = null,
        Guid? clientId = null,
        CancellationToken cancellationToken = default) =>
        AddAsync(new AiBoundaryAuditEvent
        {
            Id = Guid.NewGuid(),
            CorrelationId = correlationId,
            Service = service,
            Operation = operation,
            Phase = "Received",
            TargetService = DefaultTargetService,
            MetadataJson = metadataJson,
            DurationMs = durationMs,
            StatusCode = statusCode,
            Success = true,
            ClientId = clientId,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

    public Task LogFailedAsync(
        string service,
        string operation,
        string correlationId,
        long durationMs,
        string errorSummary,
        int? statusCode = null,
        Guid? clientId = null,
        CancellationToken cancellationToken = default) =>
        AddAsync(new AiBoundaryAuditEvent
        {
            Id = Guid.NewGuid(),
            CorrelationId = correlationId,
            Service = service,
            Operation = operation,
            Phase = "Failed",
            TargetService = DefaultTargetService,
            MetadataJson = "{}",
            DurationMs = durationMs,
            StatusCode = statusCode,
            Success = false,
            ErrorSummary = errorSummary.Length > 500 ? errorSummary[..500] : errorSummary,
            ClientId = clientId,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

    public async Task<IReadOnlyList<AiBoundaryAuditEventDto>> GetRecentAsync(
        int? limit,
        string? correlationId,
        string? operation,
        bool? success,
        CancellationToken cancellationToken = default)
    {
        IQueryable<AiBoundaryAuditEvent> query = _db.AiBoundaryAuditEvents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(correlationId))
            query = query.Where(e => e.CorrelationId == correlationId);
        if (!string.IsNullOrWhiteSpace(operation))
            query = query.Where(e => e.Operation == operation);
        if (success.HasValue)
            query = query.Where(e => e.Success == success.Value);

        query = query.OrderByDescending(e => e.CreatedAt);
        var list = limit.HasValue ? await query.Take(limit.Value).ToListAsync(cancellationToken) : await query.ToListAsync(cancellationToken);

        return list.Select(e => new AiBoundaryAuditEventDto
        {
            Id = e.Id,
            CorrelationId = e.CorrelationId,
            Service = e.Service,
            Operation = e.Operation,
            Phase = e.Phase,
            TargetService = e.TargetService,
            MetadataJson = e.MetadataJson,
            DurationMs = e.DurationMs,
            StatusCode = e.StatusCode,
            Success = e.Success,
            ErrorSummary = e.ErrorSummary,
            ClientId = e.ClientId,
            Timestamp = e.CreatedAt
        }).ToList();
    }

    // A failed write to the audit log must never take down the call site it's auditing —
    // that would make "log the AI boundary" itself a new way to break the report-generation
    // flow it's supposed to be watching. Swallow and let the caller's own ILogger calls
    // (which already run at every one of these call sites) carry the failure instead.
    private async Task AddAsync(AiBoundaryAuditEvent evt, CancellationToken cancellationToken)
    {
        try
        {
            _db.AiBoundaryAuditEvents.Add(evt);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Intentionally swallowed — see method comment.
        }
    }
}
