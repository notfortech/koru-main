using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
            .Validate(
                opts => !string.IsNullOrWhiteSpace(opts.BaseUrl),
                "AgentHost:BaseUrl is required. Set the AGENT_HOST_BASE_URL environment variable.")
            .ValidateOnStart();

        // ── Typed HTTP client with Polly retry + circuit breaker ───────────────
        // Must be keyed to the INTERFACE so that IHttpClientFactory's ConfigureHttpClient
        // delegate (which sets BaseAddress) runs whenever IAgentHostClient is resolved.
        // Using AddHttpClient<AgentHostClient> + AddScoped<IAgentHostClient, AgentHostClient>
        // bypasses the factory: AddScoped resolves AgentHostClient via standard DI, injecting
        // a plain HttpClient with BaseAddress = null.
        services.AddHttpClient<IAgentHostClient, AgentHostClient>()
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
            // ── Configurable circuit breaker ──────────────────────────────────
            .AddPolicyHandler((sp, _) =>
            {
                var opts = sp.GetRequiredService<IOptionsMonitor<AgentHostOptions>>().CurrentValue;
                return opts.EnableCircuitBreaker
                    ? BuildCircuitBreakerPolicy(opts.CircuitFailureThreshold, opts.CircuitBreakDurationSeconds)
                    : Policy.NoOpAsync<HttpResponseMessage>();
            });

        // ── Health check ───────────────────────────────────────────────────────
        services.AddHealthChecks()
            .AddCheck<AgentHostHealthCheck>(
                name: "agenthost",
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
                tags: new[] { "agenthost", "ready" });

        // ── Interfaces → Implementations ───────────────────────────────────────
        // IAgentHostClient is already registered by AddHttpClient<IAgentHostClient, AgentHostClient>()
        // above — no separate AddScoped needed (that would re-register the interface via plain DI,
        // bypassing the factory and losing BaseAddress).
        services.AddSingleton<IBlueprintGenerationQueue, BlueprintGenerationQueue>();
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

    private static IAsyncPolicy<HttpResponseMessage> BuildCircuitBreakerPolicy(
        int failureThreshold,
        int breakDurationSeconds) =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: failureThreshold > 0 ? failureThreshold : 5,
                durationOfBreak: TimeSpan.FromSeconds(breakDurationSeconds > 0 ? breakDurationSeconds : 30));
}
