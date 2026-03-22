using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.Infrastructure.Services;

public class ReportingTechnicalLogWriter : IReportingTechnicalLogWriter
{
    private readonly ApplicationDbContext _db;

    public ReportingTechnicalLogWriter(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(string service, string level, string message, string? stackTrace = null, CancellationToken cancellationToken = default)
    {
        var entry = new ReportingTechnicalLog
        {
            LogId = Guid.NewGuid(),
            Service = service?.Length > 200 ? service.Substring(0, 200) : service,
            Level = level?.Length > 50 ? level.Substring(0, 50) : level,
            Message = message?.Length > 4000 ? message.Substring(0, 4000) : message,
            StackTrace = stackTrace?.Length > 8000 ? stackTrace.Substring(0, 8000) : stackTrace,
            CreatedDate = DateTime.UtcNow
        };
        _db.ReportingTechnicalLogs.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
