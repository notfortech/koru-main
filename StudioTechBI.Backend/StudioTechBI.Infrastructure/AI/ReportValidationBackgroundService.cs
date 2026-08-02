using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.ReportGenerator;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Services;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.Infrastructure.AI;

/// <summary>
/// Long-running background worker that drains the ReportValidationQueue. Each run: Data Sanity
/// first (cheap, in-process, classifies the already-submitted report snapshot), then Rendering
/// Health (expensive, calls out to the Playwright-based DashboardAgents.ReportValidationApi).
/// Uses IServiceScopeFactory to obtain a scoped ApplicationDbContext per job — same pattern as
/// BlueprintGenerationBackgroundService.
/// </summary>
public sealed class ReportValidationBackgroundService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly TimeSpan RenderingHealthTokenLifetime = TimeSpan.FromMinutes(10);

    private readonly IReportValidationQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReportValidationBackgroundService> _logger;

    public ReportValidationBackgroundService(
        IReportValidationQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<ReportValidationBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReportValidationBackgroundService starting.");

        // On restart, re-queue any runs that were stuck in Pending/Processing.
        await RequeueStuckRunsAsync(stoppingToken);

        await foreach (var runId in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(runId, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex,
                    "Unhandled exception in report validation worker for RunId={RunId}.", runId);
            }
        }
    }

    private async Task ProcessAsync(Guid runId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var scratchStorage = scope.ServiceProvider.GetRequiredService<IReportValidationScratchStorageService>();
        var reportValidationClient = scope.ServiceProvider.GetRequiredService<IReportValidationClient>();
        var jwtTokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        var run = await db.ReportValidationRuns
            .Include(r => r.Checks)
            .FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null)
        {
            _logger.LogWarning("ReportValidationRun {RunId} not found — skipping.", runId);
            return;
        }

        run.Status = ReportValidationStatuses.Processing;
        run.ProcessingStartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("ReportValidation.Started RunId={RunId} ClientId={ClientId}", runId, run.ClientId);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // ── 1. Data sanity (cheap, in-process, no external call) ──────────────────────
            var dto = JsonSerializer.Deserialize<GeneratedReportDto>(run.ReportSnapshotJson, JsonOptions)
                ?? throw new InvalidOperationException("ReportValidationRun.ReportSnapshotJson could not be deserialised.");

            var allChecks = new List<ReportValidationCheck>();

            var dataSanityChecks = ReportDataSanityClassifier.Classify(dto);
            foreach (var check in dataSanityChecks)
            {
                check.ReportValidationRunId = run.Id;
                db.ReportValidationChecks.Add(check);
                allChecks.Add(check);
            }
            await db.SaveChangesAsync(ct);

            // ── 2. Rendering health (expensive, browser-based) ─────────────────────────────
            if (string.IsNullOrWhiteSpace(run.SourceFileScratchBlobPath))
            {
                var unavailable = new ReportValidationCheck
                {
                    Id = Guid.NewGuid(),
                    ReportValidationRunId = run.Id,
                    CheckFamily = ReportValidationCheckFamilies.RenderingHealth,
                    CheckName = "rendering-health-unavailable",
                    Status = ReportValidationCheckStatuses.Warning,
                    Detail = "No source file was captured for this run — rendering health could not be checked.",
                    SortOrder = 0
                };
                db.ReportValidationChecks.Add(unavailable);
                allChecks.Add(unavailable);
            }
            else
            {
                var renderingHealthChecks = await RunRenderingHealthAsync(db, run, scratchStorage, reportValidationClient, jwtTokenService, ct);
                allChecks.AddRange(renderingHealthChecks);
            }
            await db.SaveChangesAsync(ct);

            // ── 3. Finalize ──────────────────────────────────────────────────────────────
            run.OverallResult = ComputeOverallResult(allChecks);
            run.Status = ReportValidationStatuses.Completed;
            run.CompletedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(run.SourceFileScratchBlobPath))
            {
                try
                {
                    await scratchStorage.DeleteAsync(run.SourceFileScratchBlobPath, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete scratch file for RunId={RunId} — non-fatal.", runId);
                }
            }

            await db.SaveChangesAsync(ct);

            sw.Stop();
            _logger.LogInformation(
                "ReportValidation.Completed RunId={RunId} OverallResult={OverallResult} DurationMs={DurationMs}",
                runId, run.OverallResult, sw.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();

            _logger.LogError(ex,
                "ReportValidation.Failed RunId={RunId} ClientId={ClientId} ErrorType={ErrorType} DurationMs={DurationMs}",
                runId, run.ClientId, ex.GetType().Name, sw.ElapsedMilliseconds);

            var statusCode = ex is HttpRequestException httpEx ? (int?)httpEx.StatusCode : null;
            var errorJson = JsonSerializer.Serialize(new
            {
                type = ex.GetType().Name,
                status = statusCode,
                msg = ex.Message.Length > 200 ? ex.Message[..200] : ex.Message,
                runId,
                durationMs = sw.ElapsedMilliseconds,
                inner = ex.InnerException?.Message is { } im
                    ? (im.Length > 100 ? im[..100] : im)
                    : null
            });

            run.Status = ReportValidationStatuses.Failed;
            run.ErrorMessage = errorJson;
            run.CompletedAt = DateTime.UtcNow;

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Failed to persist Failed status for RunId={RunId}.", runId);
            }
        }
    }

    private async Task<List<ReportValidationCheck>> RunRenderingHealthAsync(
        ApplicationDbContext db,
        ReportValidationRun run,
        IReportValidationScratchStorageService scratchStorage,
        IReportValidationClient reportValidationClient,
        IJwtTokenService jwtTokenService,
        CancellationToken ct)
    {
        var results = new List<ReportValidationCheck>();
        try
        {
            await using var fileStream = await scratchStorage.GetAsync(run.SourceFileScratchBlobPath!, ct)
                ?? throw new InvalidOperationException("Scratch source file is missing.");

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == run.RequestedByUserId, ct);
            var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == run.ClientId, ct);
            var token = jwtTokenService.GenerateAccessToken(BuildRunClaims(run, user, client), RenderingHealthTokenLifetime);

            var fileName = Path.GetFileName(run.SourceFileScratchBlobPath!);
            var response = await reportValidationClient.RunRenderingHealthAsync(
                fileStream, fileName, run.TemplateId, run.FiltersJson, token, run.Id.ToString(), ct);

            var sortOrder = 0;
            foreach (var check in response.Checks)
            {
                results.Add(new ReportValidationCheck
                {
                    Id = Guid.NewGuid(),
                    ReportValidationRunId = run.Id,
                    CheckFamily = ReportValidationCheckFamilies.RenderingHealth,
                    CheckName = check.Name,
                    Status = check.Status,
                    Detail = check.Detail,
                    EvidenceJson = check.Evidence is { Count: > 0 } ? JsonSerializer.Serialize(check.Evidence) : null,
                    SortOrder = sortOrder++
                });
            }

            if (response.Checks.Count == 0)
            {
                results.Add(new ReportValidationCheck
                {
                    Id = Guid.NewGuid(),
                    ReportValidationRunId = run.Id,
                    CheckFamily = ReportValidationCheckFamilies.RenderingHealth,
                    CheckName = "no-checks-returned",
                    Status = ReportValidationCheckStatuses.Warning,
                    Detail = "The rendering health service returned no check results.",
                    SortOrder = 0
                });
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The report itself might be fine — we just couldn't run the browser check. Distinct
            // from a Fail so the UI can tell "the report is broken" from "we couldn't check."
            _logger.LogWarning(ex, "Rendering health check could not be run for RunId={RunId}.", run.Id);
            results.Add(new ReportValidationCheck
            {
                Id = Guid.NewGuid(),
                ReportValidationRunId = run.Id,
                CheckFamily = ReportValidationCheckFamilies.RenderingHealth,
                CheckName = "rendering-health-error",
                Status = ReportValidationResults.Error,
                Detail = $"Could not run rendering health check: {ex.Message}",
                SortOrder = 0
            });
        }

        db.ReportValidationChecks.AddRange(results);
        return results;
    }

    /// <summary>Minimal claim set for a short-lived, purpose-scoped token — not the requesting
    /// user's own session token. The 10-minute expiry is the real blast-radius control; the
    /// "purpose" claim is an audit-trail marker only, not enforced by any authorization policy.</summary>
    private static List<Claim> BuildRunClaims(ReportValidationRun run, User? user, Client? client)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, run.RequestedByUserId.ToString()),
            new("purpose", "report-validation")
        };

        if (user is not null)
            claims.Add(new Claim(ClaimTypes.Email, user.Email));

        var folderName = client?.ClientCode ?? client?.BlobFolderPath;
        if (!string.IsNullOrEmpty(folderName))
            claims.Add(new Claim("client_code", folderName));

        return claims;
    }

    private static string ComputeOverallResult(IEnumerable<ReportValidationCheck> checks)
    {
        var statuses = checks.Select(c => c.Status).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (statuses.Contains(ReportValidationCheckStatuses.Fail)) return ReportValidationResults.Fail;
        if (statuses.Contains(ReportValidationResults.Error)) return ReportValidationResults.Error;
        if (statuses.Contains(ReportValidationCheckStatuses.Warning)) return ReportValidationResults.Warning;
        return ReportValidationResults.Pass;
    }

    private async Task RequeueStuckRunsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var stuck = await db.ReportValidationRuns
                .Where(r => r.Status == ReportValidationStatuses.Pending || r.Status == ReportValidationStatuses.Processing)
                .Select(r => r.Id)
                .ToListAsync(ct);

            foreach (var id in stuck)
                await _queue.EnqueueAsync(id, ct);

            if (stuck.Count > 0)
                _logger.LogInformation("Re-queued {Count} stuck report validation run(s) on startup.", stuck.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not re-queue stuck report validation runs on startup.");
        }
    }
}
