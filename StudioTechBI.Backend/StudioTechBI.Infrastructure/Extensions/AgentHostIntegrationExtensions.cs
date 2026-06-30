using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;
using StudioTechBI.Infrastructure.AI;
using StudioTechBI.Infrastructure.Clients;
using StudioTechBI.Infrastructure.Repositories;
using StudioTechBI.Infrastructure.Services;

namespace StudioTechBI.Infrastructure.Extensions;

/// <summary>
/// Registers all AI Gateway services.
/// Program.cs calls builder.Services.AddAgentHostIntegration() — nothing else.
/// </summary>
public static class AgentHostIntegrationExtensions
{
    public static IServiceCollection AddAgentHostIntegration(this IServiceCollection services)
    {
        // ── Configuration ──────────────────────────────────────────────────────
        services.AddOptions<AgentHostOptions>()
            .BindConfiguration(AgentHostOptions.SectionName)
            .ValidateOnStart();

        // ── Typed HTTP client with Polly retry + circuit breaker ───────────────
        services.AddHttpClient<AgentHostClient>()
            .ConfigureHttpClient((sp, client) =>
            {
                var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<AgentHostOptions>>()
                             .CurrentValue;

                if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
                    client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");

                client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds > 0 ? opts.TimeoutSeconds : 300);

                // API Key as Bearer token (supports future migration to Managed Identity)
                if (!string.IsNullOrWhiteSpace(opts.ApiKey))
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.ApiKey);
            })
            .AddPolicyHandler((sp, _) =>
            {
                var retryCount = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<AgentHostOptions>>()
                                   .CurrentValue.RetryCount;
                return BuildRetryPolicy(retryCount > 0 ? retryCount : 3);
            })
            .AddPolicyHandler(BuildCircuitBreakerPolicy());

        // ── Interfaces → Implementations ───────────────────────────────────────
        services.AddSingleton<IBlueprintGenerationQueue, BlueprintGenerationQueue>();
        services.AddScoped<IAgentHostClient, AgentHostClient>();
        services.AddScoped<IBlueprintRepository, BlueprintRepository>();
        services.AddScoped<IBlueprintStorageService, BlueprintStorageService>();
        services.AddScoped<IAiGateway, AiGateway>();

        // ── Background worker ──────────────────────────────────────────────────
        services.AddHostedService<BlueprintGenerationBackgroundService>();

        return services;
    }

    // ── Polly policies ─────────────────────────────────────────────────────────

    private static IAsyncPolicy<HttpResponseMessage> BuildRetryPolicy(int retryCount) =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => (int)r.StatusCode == 429)
            .WaitAndRetryAsync(
                retryCount,
                attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))
                         + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 300)));

    private static IAsyncPolicy<HttpResponseMessage> BuildCircuitBreakerPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30));
}
