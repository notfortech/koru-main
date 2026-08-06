using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.Infrastructure.Services;

/// <summary>
/// Keeps DashboardAgents.ReportAgent.Api's HTML template manifest cache warm on a fixed interval —
/// that service has no outbound network access of its own (see PythonAgentRunner's threat-model
/// comments), so its registry is kept up to date by this push, not by a pull it would have to make
/// itself. The actual sync logic lives in <see cref="IHtmlTemplateSyncRunner"/> so it can also be
/// triggered on demand (e.g. right after an admin upload/edit) without waiting on this timer.
/// </summary>
public sealed class HtmlTemplateRegistrySyncService : BackgroundService
{
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
                using var scope = _serviceProvider.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<IHtmlTemplateSyncRunner>();
                await runner.RunOnceAsync(stoppingToken);
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
}
