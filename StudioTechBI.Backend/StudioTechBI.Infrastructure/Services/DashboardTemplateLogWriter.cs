using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.Infrastructure.Services;

public class DashboardTemplateLogWriter : IDashboardTemplateLogWriter
{
    // Must match FunctionalLogConfiguration's HasMaxLength(2000) on the real
    // nvarchar(2000) Description column — a larger cap here throws a truncation
    // SqlException on every write with a non-trivial log (i.e. nearly every one).
    private const int MaxDescriptionLength = 2000;

    private readonly ApplicationDbContext _db;

    public DashboardTemplateLogWriter(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(
        Guid? clientId,
        string clientName,
        bool success,
        string summary,
        IReadOnlyList<string> logLines,
        CancellationToken cancellationToken = default)
    {
        var description = $"[{clientName}] {summary}";
        if (logLines.Count > 0)
            description += "\n\n" + string.Join("\n", logLines);
        if (description.Length > MaxDescriptionLength)
            description = string.Concat(description.AsSpan(0, MaxDescriptionLength), "…");

        _db.FunctionalLogs.Add(new FunctionalLog
        {
            Id = Guid.NewGuid(),
            EventType = success ? "DashboardTemplateGenerated" : "DashboardTemplateGenerationFailed",
            ClientId = clientId,
            Description = description,
            Timestamp = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(cancellationToken);
    }
}
