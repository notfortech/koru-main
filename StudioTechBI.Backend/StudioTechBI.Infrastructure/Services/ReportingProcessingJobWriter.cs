using Microsoft.EntityFrameworkCore;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.Infrastructure.Services;

public class ReportingProcessingJobWriter : IReportingProcessingJobWriter
{
    private readonly ApplicationDbContext _db;

    public ReportingProcessingJobWriter(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task StartAsync(Guid jobId, Guid? clientId, string? fileName, string? blobPath, CancellationToken cancellationToken = default)
    {
        _db.ReportingProcessingJobs.Add(new ReportingProcessingJob
        {
            JobId = jobId,
            ClientId = clientId,
            FileName = fileName?.Length > 500 ? fileName.Substring(0, 500) : fileName,
            BlobPath = blobPath?.Length > 1000 ? blobPath.Substring(0, 1000) : blobPath,
            Status = "Started",
            CurrentStep = "ValidateUploads",
            CreatedDate = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Guid jobId, string status, string? currentStep = null, string? errorMessage = null, DateTime? completedDate = null, CancellationToken cancellationToken = default)
    {
        var job = await _db.ReportingProcessingJobs.FirstOrDefaultAsync(j => j.JobId == jobId, cancellationToken);
        if (job == null) return;
        job.Status = status?.Length > 100 ? status.Substring(0, 100) : status;
        if (currentStep != null) job.CurrentStep = currentStep.Length > 200 ? currentStep.Substring(0, 200) : currentStep;
        if (errorMessage != null) job.ErrorMessage = errorMessage.Length > 2000 ? errorMessage.Substring(0, 2000) : errorMessage;
        if (completedDate.HasValue) job.CompletedDate = completedDate;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
