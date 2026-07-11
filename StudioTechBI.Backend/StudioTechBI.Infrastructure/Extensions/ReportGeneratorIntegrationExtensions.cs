using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;
using StudioTechBI.Infrastructure.Clients;

namespace StudioTechBI.Infrastructure.Extensions;

public static class ReportGeneratorIntegrationExtensions
{
    public static IServiceCollection AddReportGeneratorIntegration(this IServiceCollection services)
    {
        services.AddOptions<ReportGeneratorOptions>()
            .BindConfiguration(ReportGeneratorOptions.SectionName);

        services.AddHttpClient<IReportGeneratorClient, ReportGeneratorClient>()
            .ConfigureHttpClient((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptionsMonitor<ReportGeneratorOptions>>().CurrentValue;

                if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
                    client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");

                client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds > 0 ? opts.TimeoutSeconds : 120);

                if (!string.IsNullOrWhiteSpace(opts.ApiKey))
                    client.DefaultRequestHeaders.Add("X-Api-Key", opts.ApiKey);
            })
            .AddPolicyHandler(BuildRetryPolicy(2));

        return services;
    }

    // Report generation can involve large files / real aggregation work, so
    // only retry idempotent-safe transient failures — never retry a POST
    // that may have already run the (potentially slow) engine to completion.
    private static IAsyncPolicy<HttpResponseMessage> BuildRetryPolicy(int retryCount) =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => (int)r.StatusCode == 429)
            .WaitAndRetryAsync(
                retryCount,
                attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))
                         + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 200)));
}
