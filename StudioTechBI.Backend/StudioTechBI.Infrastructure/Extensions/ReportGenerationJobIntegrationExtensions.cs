using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Infrastructure.AI;
using StudioTechBI.Infrastructure.Services;

namespace StudioTechBI.Infrastructure.Extensions;

/// <summary>Registers the large-file Report Generator async pipeline (Phase 1) — the durable
/// Azure Storage Queue and its background worker. No new HttpClient here: the worker reuses the
/// already-registered IReportGeneratorClient to do the actual generate call.</summary>
public static class ReportGenerationJobIntegrationExtensions
{
    public static IServiceCollection AddReportGenerationJobIntegration(this IServiceCollection services)
    {
        services.AddSingleton<IReportGenerationJobQueue, ReportGenerationJobQueue>();
        services.AddHostedService<ReportGenerationJobBackgroundService>();

        return services;
    }
}
