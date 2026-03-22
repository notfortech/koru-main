using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.Infrastructure.Services;

public class ProcessingJobService : IProcessingJobService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ProcessingJobService> _logger;

    public ProcessingJobService(ApplicationDbContext db, ILogger<ProcessingJobService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProcessingJobDto>> GetAllAsync(int? limit, CancellationToken cancellationToken = default)
    {
        var query = _db.ProcessingJobs
            .AsNoTracking()
            .Include(j => j.Client)
            .OrderByDescending(j => j.CreatedDate);
        var list = limit.HasValue ? await query.Take(limit.Value).ToListAsync(cancellationToken) : await query.ToListAsync(cancellationToken);
        return list.Select(MapToDto).ToList();
    }

    public async Task<ProcessingJobDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _db.ProcessingJobs
            .AsNoTracking()
            .Include(j => j.Client)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        return job == null ? null : MapToDto(job);
    }

    private static ProcessingJobDto MapToDto(Domain.Entities.ProcessingJob j)
    {
        return new ProcessingJobDto
        {
            JobId = j.Id,
            ClientId = j.ClientId,
            ClientName = j.Client?.ClientName,
            FileName = j.FileName,
            Status = j.Status.ToString(),
            ErrorMessage = j.ErrorMessage,
            CreatedDate = j.CreatedDate
        };
    }
}
