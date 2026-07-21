using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;
using StudioTechBI.Infrastructure.Clients;

namespace StudioTechBI.Infrastructure.Extensions;

/// <summary>
/// New, standalone registration for IPbipImportClient. Reuses BindDeployOptions unmodified — the
/// pbip-import endpoint lives on the same stbi-bind-deploy service, same base URL and API key —
/// same pattern as TemplateRebindIntegrationExtensions.
/// </summary>
public static class PbipImportIntegrationExtensions
{
    public static IServiceCollection AddPbipImportIntegration(this IServiceCollection services)
    {
        services.AddOptions<BindDeployOptions>()
            .BindConfiguration(BindDeployOptions.SectionName);
        services.AddOptions<DashboardTemplateOptions>()
            .BindConfiguration(DashboardTemplateOptions.SectionName);

        services.AddHttpClient<IPbipImportClient, PbipImportClient>()
            .ConfigureHttpClient((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptionsMonitor<BindDeployOptions>>().CurrentValue;

                if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
                {
                    if (Uri.TryCreate(opts.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri))
                    {
                        client.BaseAddress = baseUri;
                    }
                    else
                    {
                        sp.GetRequiredService<ILoggerFactory>()
                            .CreateLogger("PbipImportIntegration")
                            .LogError(
                                "BindDeploy:BaseUrl is not a valid absolute URI: '{BaseUrl}'.",
                                opts.BaseUrl);
                    }
                }

                // Import polling alone can take up to ~60s server-side — give this client extra
                // headroom over the default 60s rather than reusing a shorter generic timeout.
                var timeoutSeconds = Math.Max(opts.TimeoutSeconds, 120);
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

                if (!string.IsNullOrWhiteSpace(opts.ApiKey))
                    client.DefaultRequestHeaders.Add("X-Service-Api-Key", opts.ApiKey);
            });

        return services;
    }
}
