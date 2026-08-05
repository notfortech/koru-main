using Microsoft.EntityFrameworkCore;
using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Domain.Enums;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.Infrastructure.Services;

public class AdminLoggingService : IAdminLoggingService
{
    private readonly ApplicationDbContext _db;

    public AdminLoggingService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<FunctionalLogDto>> GetFunctionalLogsAsync(int? limit, Guid? clientId, CancellationToken cancellationToken = default)
    {
        IQueryable<Domain.Entities.FunctionalLog> query = _db.FunctionalLogs.AsNoTracking();
        if (clientId.HasValue)
            query = query.Where(l => l.ClientId == clientId.Value);
        query = query.OrderByDescending(l => l.Timestamp);
        var list = limit.HasValue ? await query.Take(limit.Value).ToListAsync(cancellationToken) : await query.ToListAsync(cancellationToken);
        return list.Select(l => new FunctionalLogDto
        {
            LogId = l.Id,
            EventType = l.EventType,
            ClientId = l.ClientId,
            Description = l.Description,
            Timestamp = l.Timestamp
        }).ToList();
    }

    public async Task<IReadOnlyList<TechnicalLogDto>> GetTechnicalLogsAsync(int? limit, string? level, CancellationToken cancellationToken = default)
    {
        IQueryable<Domain.Entities.TechnicalLog> query = _db.TechnicalLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(level) && Enum.TryParse<Domain.Enums.TechnicalLogLevel>(level, true, out var lvl))
            query = query.Where(l => l.Level == lvl);
        query = query.OrderByDescending(l => l.Timestamp);
        var list = limit.HasValue ? await query.Take(limit.Value).ToListAsync(cancellationToken) : await query.ToListAsync(cancellationToken);
        return list.Select(l => new TechnicalLogDto
        {
            LogId = l.Id,
            Service = l.Service,
            Level = l.Level.ToString(),
            Message = l.Message,
            StackTrace = l.StackTrace,
            Timestamp = l.Timestamp
        }).ToList();
    }

    public async Task<IReadOnlyList<FunctionalLogDto>> GetDashboardTemplateLogsAsync(int? limit, Guid? clientId, CancellationToken cancellationToken = default)
    {
        IQueryable<Domain.Entities.FunctionalLog> query = _db.FunctionalLogs.AsNoTracking()
            .Where(l => l.EventType == "DashboardTemplateGenerated"
                || l.EventType == "DashboardTemplateGenerationFailed"
                || l.EventType == "DashboardTemplateBuildRequested"
                || l.EventType == "DashboardTemplateMatchCheckFailed"
                || l.EventType == "HtmlTemplateBuildRequested");
        if (clientId.HasValue)
            query = query.Where(l => l.ClientId == clientId.Value);
        query = query.OrderByDescending(l => l.Timestamp);
        var list = limit.HasValue ? await query.Take(limit.Value).ToListAsync(cancellationToken) : await query.ToListAsync(cancellationToken);
        return list.Select(l => new FunctionalLogDto
        {
            LogId = l.Id,
            EventType = l.EventType,
            ClientId = l.ClientId,
            Description = l.Description,
            Timestamp = l.Timestamp
        }).ToList();
    }

    public async Task LogAdminActionAsync(string service, string message, CancellationToken cancellationToken = default)
    {
        _db.TechnicalLogs.Add(new TechnicalLog
        {
            Id = Guid.NewGuid(),
            Service = service,
            Level = TechnicalLogLevel.Information,
            Message = message,
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
    }
}
