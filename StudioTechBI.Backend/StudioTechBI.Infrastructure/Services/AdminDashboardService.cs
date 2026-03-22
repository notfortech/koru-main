using Microsoft.EntityFrameworkCore;
using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Enums;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.Infrastructure.Services;

public class AdminDashboardService : IAdminDashboardService
{
    private readonly ApplicationDbContext _db;

    public AdminDashboardService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var totalClients = await _db.Clients.CountAsync(c => !c.IsDeleted, cancellationToken);
        var activeClients = await _db.Clients.CountAsync(c => !c.IsDeleted && c.IsActive, cancellationToken);
        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var uploadsThisMonth = await _db.ProcessingJobs.CountAsync(j => j.CreatedDate >= startOfMonth, cancellationToken);
        var failedValidations = await _db.ProcessingJobs.CountAsync(j => j.Status == JobStatus.Failed, cancellationToken);
        var lastRefresh = await _db.ProcessingJobs
            .Where(j => j.Status == JobStatus.Completed)
            .OrderByDescending(j => j.CreatedDate)
            .Select(j => (DateTime?)j.CreatedDate)
            .FirstOrDefaultAsync(cancellationToken);

        return new DashboardDto
        {
            TotalClients = totalClients,
            ActiveClients = activeClients,
            UploadsThisMonth = uploadsThisMonth,
            FailedValidations = failedValidations,
            LastDatasetRefresh = lastRefresh
        };
    }
}
