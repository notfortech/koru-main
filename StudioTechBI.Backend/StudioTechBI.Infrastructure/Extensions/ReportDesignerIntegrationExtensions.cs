using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;
using StudioTechBI.Infrastructure.Clients;
using StudioTechBI.Infrastructure.Services;

namespace StudioTechBI.Infrastructure.Extensions;

public static class ReportDesignerIntegrationExtensions
{
    public static IServiceCollection AddReportDesignerIntegration(this IServiceCollection services)
    {
        services.AddOptions<ReportDesignerOptions>()
            .BindConfiguration(ReportDesignerOptions.SectionName);

        services.AddOptions<AzureAdOptions>()
            .BindConfiguration(AzureAdOptions.SectionName);

        services.AddHttpClient<IReportDesignerClient, ReportDesignerClient>()
            .ConfigureHttpClient((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptionsMonitor<ReportDesignerOptions>>().CurrentValue;

                if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
                    client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");

                client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds > 0 ? opts.TimeoutSeconds : 120);

                // API key as Bearer token
                if (!string.IsNullOrWhiteSpace(opts.ApiKey))
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.ApiKey);
            })
            .AddPolicyHandler(BuildRetryPolicy(2));

        services.AddScoped<SqlSchemaReaderService>();
        services.AddScoped<SharePointSchemaService>();

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
