using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.ReportGenerator;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.Infrastructure.Services;

/// <summary>
/// Keeps DashboardAgents.ReportAgent.Api's HTML template manifest cache warm — that service has
/// no outbound network access of its own (see PythonAgentRunner's threat-model comments), so its
/// registry is kept up to date by this push, not by a pull it would have to make itself.
///
/// Discovery: rather than add a generic "list blobs by prefix" capability to the shared
/// IBlobStorageService (used everywhere else in the app for simple path-addressed reads/writes),
/// this reuses the exact registration convention the JSON computation templates already use —
/// templates/html/index.json is a small, author-maintained list of template ids, fetched via the
/// existing DownloadBlobAsync(path) the same way every other blob read in this codebase works.
/// </summary>
public sealed class HtmlTemplateRegistrySyncService : BackgroundService
{
    private const string IndexBlobPath = "templates/html/index.json";
    private static readonly TimeSpan SyncInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HtmlTemplateRegistrySyncService> _logger;

    public HtmlTemplateRegistrySyncService(IServiceProvider serviceProvider, ILogger<HtmlTemplateRegistrySyncService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Syncs once immediately (so a cold container isn't stuck matching against nothing for up
        // to 5 minutes), then on the TTL. A failed cycle never crashes the host and never clears
        // whatever ReportAgent.Api already has cached -- it just tries again next cycle, per the
        // "acceptable staleness for a template catalog that changes rarely" design decision.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "HtmlTemplateRegistrySync.CycleFailed — keeping the previously synced registry.");
            }

            try
            {
                await Task.Delay(SyncInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task SyncOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var blobStorage = scope.ServiceProvider.GetRequiredService<IBlobStorageService>();
        var reportGeneratorClient = scope.ServiceProvider.GetRequiredService<IReportGeneratorClient>();

        List<string>? templateIds;
        await using (var indexStream = await blobStorage.DownloadBlobAsync(IndexBlobPath, cancellationToken))
        {
            if (indexStream is null)
            {
                _logger.LogInformation("HtmlTemplateRegistrySync.NoIndex — {Path} not found yet, nothing to sync.", IndexBlobPath);
                return;
            }

            templateIds = await JsonSerializer.DeserializeAsync<List<string>>(indexStream, cancellationToken: cancellationToken);
        }

        if (templateIds is null || templateIds.Count == 0)
            return;

        var manifests = new List<HtmlTemplateManifestPushDto>();
        foreach (var id in templateIds)
        {
            var manifestPath = $"templates/html/{id}/manifest.json";
            await using var manifestStream = await blobStorage.DownloadBlobAsync(manifestPath, cancellationToken);
            if (manifestStream is null)
            {
                _logger.LogWarning(
                    "HtmlTemplateRegistrySync.ManifestMissing TemplateId={TemplateId} Path={Path} — listed in " +
                    "the index but no manifest.json found; skipped for this cycle.",
                    id, manifestPath);
                continue;
            }

            using var reader = new StreamReader(manifestStream, Encoding.UTF8);
            var manifestJson = await reader.ReadToEndAsync(cancellationToken);
            manifests.Add(new HtmlTemplateManifestPushDto(id, manifestJson));
        }

        if (manifests.Count == 0)
        {
            _logger.LogWarning("HtmlTemplateRegistrySync.NoManifestsResolved — index listed {Count} id(s), none resolved.", templateIds.Count);
            return;
        }

        await reportGeneratorClient.PushHtmlTemplateRegistryAsync(
            manifests, correlationId: Guid.NewGuid().ToString("N"), cancellationToken);

        _logger.LogInformation("HtmlTemplateRegistrySync.Pushed Count={Count}", manifests.Count);
    }
}
