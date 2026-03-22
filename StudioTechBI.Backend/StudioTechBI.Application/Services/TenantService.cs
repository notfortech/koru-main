using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Domain.Interfaces;

namespace StudioTechBI.Application.Services;

public class TenantService : BaseService, ITenantService
{
    private readonly IRepository<Tenant> _tenantRepository;
    private readonly IAuditLogService _auditLogService;
    private readonly IAdminLoggingService _adminLogging;

    public TenantService(
        IUnitOfWork unitOfWork,
        IRepository<Tenant> tenantRepository,
        IAuditLogService auditLogService,
        IAdminLoggingService adminLogging)
        : base(unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _auditLogService = auditLogService;
        _adminLogging = adminLogging;
    }

    public async Task<TenantDto> CreateAsync(TenantCreateDto dto, CancellationToken cancellationToken = default)
    {
        var code = (dto.Code ?? "").Trim();
        if (string.IsNullOrEmpty(code))
            throw new InvalidOperationException("Tenant Code is required.");
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new InvalidOperationException("Tenant Name is required.");

        var existing = await _tenantRepository.FirstOrDefaultAsync(t => t.Code == code, cancellationToken);
        if (existing != null)
            throw new InvalidOperationException($"A tenant with code '{code}' already exists.");

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            Code = code,
            Industry = dto.Industry?.Trim(),
            Country = dto.Country?.Trim(),
            IsActive = true
        };
        await _tenantRepository.AddAsync(tenant, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogService.WriteAsync(new AuditLogEntryDto
        {
            TenantId = tenant.Id,
            Action = "TenantCreated",
            EntityType = "Tenant",
            EntityId = tenant.Id.ToString(),
            NewValue = System.Text.Json.JsonSerializer.Serialize(new { tenant.Name, tenant.Code, tenant.Industry, tenant.Country })
        }, cancellationToken);
        await _adminLogging.LogAdminActionAsync("TenantService", $"Tenant created: {tenant.Code}", cancellationToken);

        return MapToDto(tenant);
    }

    public async Task<PaginatedResult<TenantDto>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _tenantRepository.GetPagedAsync(page, pageSize, null, cancellationToken);
        return new PaginatedResult<TenantDto>
        {
            Items = items.Select(MapToDto).ToList(),
            PageNumber = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<TenantDto?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        return tenant == null ? null : MapToDto(tenant);
    }

    public async Task<TenantDto?> UpdateAsync(Guid tenantId, TenantUpdateDto dto, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant == null) return null;

        var oldValue = System.Text.Json.JsonSerializer.Serialize(new { tenant.Name, tenant.Industry, tenant.Country });
        tenant.Name = dto.Name.Trim();
        tenant.Industry = dto.Industry?.Trim();
        tenant.Country = dto.Country?.Trim();
        await _tenantRepository.UpdateAsync(tenant, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        var newValue = System.Text.Json.JsonSerializer.Serialize(new { tenant.Name, tenant.Industry, tenant.Country });
        await _auditLogService.WriteAsync(new AuditLogEntryDto
        {
            TenantId = tenantId,
            Action = "TenantUpdated",
            EntityType = "Tenant",
            EntityId = tenantId.ToString(),
            OldValue = oldValue,
            NewValue = newValue
        }, cancellationToken);
        await _adminLogging.LogAdminActionAsync("TenantService", $"Tenant updated: {tenant.Code}", cancellationToken);

        return MapToDto(tenant);
    }

    public async Task<TenantDto?> SetStatusAsync(Guid tenantId, TenantStatusDto dto, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant == null) return null;

        var previous = tenant.IsActive;
        tenant.IsActive = dto.IsActive;
        await _tenantRepository.UpdateAsync(tenant, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogService.WriteAsync(new AuditLogEntryDto
        {
            TenantId = tenantId,
            Action = "TenantStatusChanged",
            EntityType = "Tenant",
            EntityId = tenantId.ToString(),
            OldValue = previous.ToString(),
            NewValue = dto.IsActive.ToString()
        }, cancellationToken);
        await _adminLogging.LogAdminActionAsync("TenantService", $"Tenant status set: {tenant.Code} -> IsActive={dto.IsActive}", cancellationToken);

        return MapToDto(tenant);
    }

    private static TenantDto MapToDto(Tenant t)
    {
        return new TenantDto
        {
            Id = t.Id,
            Name = t.Name,
            Code = t.Code,
            Industry = t.Industry,
            Country = t.Country,
            IsActive = t.IsActive,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        };
    }
}
