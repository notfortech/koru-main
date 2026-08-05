using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.ReportGenerator;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.Infrastructure.AI;

/// <summary>
/// Drains the durable ReportGenerationJobQueue (large-file Report Generator uploads only — the
/// synchronous /generate endpoint handles everything below the configured size threshold and
/// never touches this worker at all). Mirrors ReportValidationBackgroundService's shape
/// (IServiceScopeFactory for a scoped ApplicationDbContext per job) but polls an Azure Storage
/// Queue instead of reading an in-process Channel, since this queue must survive an app restart.
/// </summary>
public sealed class ReportGenerationJobBackgroundService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Generation itself is expected to finish in well under a minute (the Python engine's own
    // wall-clock timeout is on the order of 90s) -- 30 minutes of invisibility is deliberately
    // generous headroom, not a tuned estimate, and periodic renewal isn't implemented since no
    // realistic job should need it.
    private static readonly TimeSpan VisibilityTimeout = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ReadSasValidFor = TimeSpan.FromHours(1);
    private static readonly TimeSpan PollDelayWhenEmpty = TimeSpan.FromSeconds(5);
    private const int MaxMessagesPerReceive = 8;

    private readonly IReportGenerationJobQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReportGenerationJobBackgroundService> _logger;

    public ReportGenerationJobBackgroundService(
        IReportGenerationJobQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<ReportGenerationJobBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReportGenerationJobBackgroundService starting.");

        await RequeueStuckJobsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            IReadOnlyList<ReportGenerationQueueMessage> messages;
            try
            {
                messages = await _queue.ReceiveAsync(MaxMessagesPerReceive, VisibilityTimeout, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "ReportGenerationJobBackgroundService: failed to receive from queue.");
                messages = Array.Empty<ReportGenerationQueueMessage>();
            }

            if (messages.Count == 0)
            {
                try
                {
                    await Task.Delay(PollDelayWhenEmpty, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                continue;
            }

            foreach (var message in messages)
            {
                try
                {
                    await ProcessAsync(message.JobId, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex,
                        "Unhandled exception in report generation job worker for JobId={JobId}.", message.JobId);
                }

                try
                {
                    await _queue.DeleteAsync(message, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex,
                        "Failed to delete queue message for JobId={JobId} — it will redeliver after the " +
                        "visibility timeout; the idempotent claim below makes that safe, just noisy.", message.JobId);
                }
            }
        }
    }

    private async Task ProcessAsync(Guid jobId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reportGeneratorClient = scope.ServiceProvider.GetRequiredService<IReportGeneratorClient>();
        var htmlAssembly = scope.ServiceProvider.GetRequiredService<IHtmlReportAssemblyService>();
        var sasUriProvider = scope.ServiceProvider.GetRequiredService<IBlobSasUriProvider>();
        var templateLogWriter = scope.ServiceProvider.GetRequiredService<IDashboardTemplateLogWriter>();

        var job = await db.ReportGenerationJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null)
        {
            _logger.LogWarning("ReportGenerationJob {JobId} not found — skipping (message will be deleted).", jobId);
            return;
        }

        // Idempotent claim: a redelivered message (or a job already picked up by another
        // instance) is a no-op here, not a double-process. Only a job still genuinely Pending
        // gets claimed.
        if (job.Status != ReportGenerationJobStatuses.Pending)
        {
            _logger.LogInformation(
                "ReportGenerationJob {JobId} already {Status} — skipping redelivered message.", jobId, job.Status);
            return;
        }

        job.Status = ReportGenerationJobStatuses.Processing;
        job.ProcessingStartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var correlationId = job.CorrelationId ?? job.Id.ToString();
        _logger.LogInformation(
            "ReportGenerationJob.Started JobId={JobId} ClientId={ClientId} CorrelationId={CorrelationId}",
            jobId, job.ClientId, correlationId);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var payload = string.IsNullOrWhiteSpace(job.RequestPayloadJson)
                ? new ReportGenerationJobRequestPayload()
                : JsonSerializer.Deserialize<ReportGenerationJobRequestPayload>(job.RequestPayloadJson, JsonOptions)
                    ?? new ReportGenerationJobRequestPayload();

            var fileUrl = await sasUriProvider.GetReadSasUriAsync(job.BlobPath, ReadSasValidFor, ct)
                ?? throw new InvalidOperationException($"Could not mint a read SAS URL for blob path '{job.BlobPath}'.");

            var result = await reportGeneratorClient.GenerateReportFromUrlAsync(
                fileUrl, job.FileName, payload.TemplateId, payload.Filters, payload.HtmlTemplateId, correlationId, ct);

            ReportThemeOverride? themeOverride = null;
            if (payload.ThemePrimary is not null || payload.ThemeDark is not null
                || payload.ThemeLight is not null || payload.ThemeBg is not null)
                themeOverride = new ReportThemeOverride(payload.ThemePrimary, payload.ThemeDark, payload.ThemeLight, payload.ThemeBg);

            result = await htmlAssembly.AssembleAsync(result, themeOverride, ct);

            var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == job.ClientId, ct);

            if (result.HtmlTemplateId is null)
            {
                var columnNames = result.Kpis.Select(k => k.Column)
                    .Concat(result.Charts.SelectMany(c => c.Series.Select(s => s.Name)))
                    .Distinct()
                    .ToList();
                await templateLogWriter.LogHtmlTemplateGapAsync(
                    job.ClientId, client?.ClientName ?? "Unknown", correlationId, columnNames,
                    matchPath: "Deterministic", bestConfidence: null, ct);
            }

            if (!payload.IsRefinement && client is not null)
            {
                try
                {
                    db.ReportGenerationEvents.Add(new ReportGenerationEvent
                    {
                        Id = Guid.NewGuid(),
                        ClientId = client.Id,
                        Mode = string.Equals(payload.Mode, "ai", StringComparison.OrdinalIgnoreCase)
                            ? ReportGenerationModes.AiAssisted
                            : ReportGenerationModes.Deterministic,
                        TemplateId = result.TemplateId,
                        TemplateName = result.TemplateName,
                        HtmlTemplateId = result.HtmlTemplateId,
                        HtmlTemplateName = result.HtmlTemplateName,
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to log ReportGenerationEvent for JobId={JobId}.", jobId);
                }
            }

            job.ResultJson = JsonSerializer.Serialize(result, JsonOptions);
            job.Status = ReportGenerationJobStatuses.Completed;
            job.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            sw.Stop();
            _logger.LogInformation(
                "ReportGenerationJob.Completed JobId={JobId} DurationMs={DurationMs}", jobId, sw.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            _logger.LogError(ex,
                "ReportGenerationJob.Failed JobId={JobId} ClientId={ClientId} ErrorType={ErrorType} DurationMs={DurationMs}",
                jobId, job.ClientId, ex.GetType().Name, sw.ElapsedMilliseconds);

            var statusCode = ex is HttpRequestException httpEx ? (int?)httpEx.StatusCode : null;
            var errorJson = JsonSerializer.Serialize(new
            {
                type = ex.GetType().Name,
                status = statusCode,
                msg = ex.Message.Length > 200 ? ex.Message[..200] : ex.Message,
                jobId,
                durationMs = sw.ElapsedMilliseconds
            });

            job.Status = ReportGenerationJobStatuses.Failed;
            job.ErrorMessage = errorJson;
            job.CompletedAt = DateTime.UtcNow;

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Failed to persist Failed status for JobId={JobId}.", jobId);
            }
        }
    }

    private async Task RequeueStuckJobsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var stuck = await db.ReportGenerationJobs
                .Where(j => j.Status == ReportGenerationJobStatuses.Pending || j.Status == ReportGenerationJobStatuses.Processing)
                .Select(j => j.Id)
                .ToListAsync(ct);

            foreach (var id in stuck)
            {
                // A job caught mid-Processing on the previous instance never finished claiming
                // cleanly -- reset to Pending so the idempotent claim above can pick it up again.
                await db.ReportGenerationJobs.Where(j => j.Id == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, ReportGenerationJobStatuses.Pending), ct);
                await _queue.EnqueueAsync(id, ct);
            }

            if (stuck.Count > 0)
                _logger.LogInformation("Re-queued {Count} stuck report generation job(s) on startup.", stuck.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not re-queue stuck report generation jobs on startup.");
        }
    }

    private sealed class ReportGenerationJobRequestPayload
    {
        public string? TemplateId { get; set; }
        public string? Filters { get; set; }
        public string? HtmlTemplateId { get; set; }
        public string? Mode { get; set; }
        public bool IsRefinement { get; set; }
        public string? ThemePrimary { get; set; }
        public string? ThemeDark { get; set; }
        public string? ThemeLight { get; set; }
        public string? ThemeBg { get; set; }
    }
}
