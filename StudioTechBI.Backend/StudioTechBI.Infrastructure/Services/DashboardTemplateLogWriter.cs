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

        await AddLogAsync(
            success ? "DashboardTemplateGenerated" : "DashboardTemplateGenerationFailed",
            clientId,
            description,
            cancellationToken);
    }

    public async Task LogBuildRequestAsync(
        Guid clientId,
        string clientName,
        string blobPath,
        IReadOnlyList<string> requestedColumns,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var description = $"[{clientName}] No confident published template match found " +
            $"(CorrelationId={correlationId}). Blended dataset: {blobPath}. Columns: {string.Join(", ", requestedColumns)}";

        await AddLogAsync("DashboardTemplateBuildRequested", clientId, description, cancellationToken);
    }

    public async Task LogMatchCheckFailedAsync(
        Guid clientId,
        string clientName,
        string correlationId,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        var description = $"[{clientName}] Template match check failed (CorrelationId={correlationId}): " +
            $"{exception.GetType().Name}: {exception.Message}";

        await AddLogAsync("DashboardTemplateMatchCheckFailed", clientId, description, cancellationToken);
    }

    private async Task AddLogAsync(string eventType, Guid? clientId, string description, CancellationToken cancellationToken)
    {
        if (description.Length > MaxDescriptionLength)
            description = string.Concat(description.AsSpan(0, MaxDescriptionLength), "…");

        _db.FunctionalLogs.Add(new FunctionalLog
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            ClientId = clientId,
            Description = description,
            Timestamp = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(cancellationToken);
    }
}
