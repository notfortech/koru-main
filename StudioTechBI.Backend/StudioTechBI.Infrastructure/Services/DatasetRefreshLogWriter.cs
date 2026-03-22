using Microsoft.EntityFrameworkCore;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.Infrastructure.Services;

public class DatasetRefreshLogWriter : IDatasetRefreshLogWriter
{
    private readonly ApplicationDbContext _db;

    public DatasetRefreshLogWriter(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(Guid? clientId, string? datasetName, string status, DateTime? startTime, DateTime? endTime, string? errorMessage, CancellationToken cancellationToken = default)
    {
        var entry = new DatasetRefreshLog
        {
            RefreshId = Guid.NewGuid(),
            ClientId = clientId,
            DatasetName = datasetName?.Length > 500 ? datasetName.Substring(0, 500) : datasetName,
            Status = status?.Length > 100 ? status.Substring(0, 100) : status,
            StartTime = startTime,
            EndTime = endTime,
            ErrorMessage = errorMessage?.Length > 4000 ? errorMessage.Substring(0, 4000) : errorMessage
        };
        _db.DatasetRefreshLogs.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
