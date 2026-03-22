using Microsoft.EntityFrameworkCore;
using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.Infrastructure.Services;

public class AuditLogService : IAuditLogService
{
    private readonly ApplicationDbContext _db;

    public AuditLogService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task WriteAsync(AuditLogEntryDto dto, CancellationToken cancellationToken = default)
    {
        var entry = new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = dto.TenantId,
            UserId = dto.UserId,
            Action = dto.Action,
            EntityType = dto.EntityType,
            EntityId = dto.EntityId,
            OldValue = dto.OldValue,
            NewValue = dto.NewValue,
            IpAddress = dto.IpAddress,
            UserAgent = dto.UserAgent,
            Timestamp = DateTime.UtcNow
        };
        _db.AuditLogs.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PaginatedResult<AuditLogDto>> GetPagedAsync(int page, int pageSize, Guid? userId = null, Guid? tenantId = null, string? action = null, DateTime? dateFrom = null, DateTime? dateTo = null, CancellationToken cancellationToken = default)
    {
        var query = _db.AuditLogs.AsNoTracking();
        if (userId.HasValue)
            query = query.Where(l => l.UserId == userId.Value);
        if (tenantId.HasValue)
            query = query.Where(l => l.TenantId == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(l => l.Action == action);
        if (dateFrom.HasValue)
            query = query.Where(l => l.Timestamp >= dateFrom.Value);
        if (dateTo.HasValue)
            query = query.Where(l => l.Timestamp <= dateTo.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new AuditLogDto
            {
                Id = l.Id,
                TenantId = l.TenantId,
                UserId = l.UserId,
                Action = l.Action,
                EntityType = l.EntityType,
                EntityId = l.EntityId,
                OldValue = l.OldValue,
                NewValue = l.NewValue,
                IpAddress = l.IpAddress,
                UserAgent = l.UserAgent,
                Timestamp = l.Timestamp
            })
            .ToListAsync(cancellationToken);

        return new PaginatedResult<AuditLogDto>
        {
            Items = items,
            PageNumber = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }
}
