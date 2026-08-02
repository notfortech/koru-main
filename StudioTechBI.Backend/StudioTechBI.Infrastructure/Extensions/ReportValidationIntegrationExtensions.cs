using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;
using StudioTechBI.Infrastructure.AI;
using StudioTechBI.Infrastructure.Clients;
using StudioTechBI.Infrastructure.Services;

namespace StudioTechBI.Infrastructure.Extensions;

public static class ReportValidationIntegrationExtensions
{
    public static IServiceCollection AddReportValidationIntegration(this IServiceCollection services)
    {
        services.AddOptions<ReportValidationOptions>()
            .BindConfiguration(ReportValidationOptions.SectionName);

        services.AddHttpClient<IReportValidationClient, ReportValidationClient>()
            .ConfigureHttpClient((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptionsMonitor<ReportValidationOptions>>().CurrentValue;

                if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
                    client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");

                client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds > 0 ? opts.TimeoutSeconds : 180);

                if (!string.IsNullOrWhiteSpace(opts.ApiKey))
                    client.DefaultRequestHeaders.Add("X-Api-Key", opts.ApiKey);
            })
            // A full Playwright wizard replay is expensive — retry only once, unlike the report
            // generator's transient-retry budget, so a flaky run doesn't silently double the cost.
            .AddPolicyHandler(BuildRetryPolicy(1));

        services.AddScoped<IReportValidationScratchStorageService, ReportValidationScratchStorageService>();
        services.AddSingleton<IReportValidationQueue, ReportValidationQueue>();
        services.AddHostedService<ReportValidationBackgroundService>();

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> BuildRetryPolicy(int retryCount) =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => (int)r.StatusCode == 429)
            .WaitAndRetryAsync(
                retryCount,
                attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))
                         + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 200)));
}
