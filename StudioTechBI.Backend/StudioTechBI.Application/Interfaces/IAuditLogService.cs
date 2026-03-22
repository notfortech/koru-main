using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.DTOs.Common;

namespace StudioTechBI.Application.Interfaces;

public interface IAuditLogService
{
    Task WriteAsync(AuditLogEntryDto dto, CancellationToken cancellationToken = default);
    Task<PaginatedResult<AuditLogDto>> GetPagedAsync(int page, int pageSize, Guid? userId = null, Guid? tenantId = null, string? action = null, DateTime? dateFrom = null, DateTime? dateTo = null, CancellationToken cancellationToken = default);
}
