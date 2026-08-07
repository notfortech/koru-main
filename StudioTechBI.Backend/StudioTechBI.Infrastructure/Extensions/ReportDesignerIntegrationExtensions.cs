using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;
using StudioTechBI.Infrastructure.AI;
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

                // TryCreate rather than `new Uri(...)`: this callback runs during DI construction
                // of IReportDesignerClient, which ReportDesignerController depends on for every one
                // of its actions — a malformed BaseUrl throwing here would 500 every endpoint on
                // that controller, not just the ones that actually need it. Leaving BaseAddress
                // null instead means only ReportDesignerClient's own call fails, with the clear
                // "BaseAddress is null" message it already raises.
                if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
                {
                    if (Uri.TryCreate(opts.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri))
                    {
                        client.BaseAddress = baseUri;
                    }
                    else
                    {
                        sp.GetRequiredService<ILoggerFactory>()
                            .CreateLogger("ReportDesignerIntegration")
                            .LogError(
                                "ReportDesigner:BaseUrl is not a valid absolute URI: '{BaseUrl}'. Did the env var omit the https:// scheme?",
                                opts.BaseUrl);
                    }
                }

                client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds > 0 ? opts.TimeoutSeconds : 210);

                // API key as Bearer token
                if (!string.IsNullOrWhiteSpace(opts.ApiKey))
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.ApiKey);
            })
            .AddPolicyHandler(BuildRetryPolicy(0));

        services.AddScoped<SqlSchemaReaderService>();
        services.AddScoped<SharePointSchemaService>();

        // ── Async "Data Model" generation (lets the client navigate away and come back) ────────
        services.AddSingleton<IReportModelGenerationQueue, ReportModelGenerationQueue>();
        services.AddHostedService<ReportModelGenerationBackgroundService>();

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
