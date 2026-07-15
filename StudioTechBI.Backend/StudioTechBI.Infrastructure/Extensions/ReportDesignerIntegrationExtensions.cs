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

                client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds > 0 ? opts.TimeoutSeconds : 210);

                // API key as Bearer token
                if (!string.IsNullOrWhiteSpace(opts.ApiKey))
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.ApiKey);
            })
            .AddPolicyHandler(BuildRetryPolicy(0));

        services.AddScoped<SqlSchemaReaderService>();
        services.AddScoped<SharePointSchemaService>();

        return services;
    }

    // Schema matching and model generation call out to an LLM (via stbi_transformers) and can
    // legitimately take up to ~180s. HttpClient.Timeout budgets the whole SendAsync call
    // including every retry attempt inside it, so retrying here means a slow-but-legitimate
    // LLM call gets killed and a second, equally-slow LLM call fires on top of it — doubling
    // AI cost and latency instead of smoothing over a transient failure. retryCount is 0 by
    // default for that reason; a failed call fails cleanly instead. Kept parameterised (rather
    // than deleted) in case a future caller wants bounded retries for genuine 429/503 responses
    // once per-attempt timeout is separated from total call budget.
    private static IAsyncPolicy<HttpResponseMessage> BuildRetryPolicy(int retryCount) =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => (int)r.StatusCode == 429)
            .WaitAndRetryAsync(
                retryCount,
                attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))
                         + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 200)));
}
