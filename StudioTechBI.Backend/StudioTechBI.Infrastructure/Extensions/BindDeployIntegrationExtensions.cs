using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;
using StudioTechBI.Infrastructure.Clients;

namespace StudioTechBI.Infrastructure.Extensions;

public static class BindDeployIntegrationExtensions
{
    public static IServiceCollection AddBindDeployIntegration(this IServiceCollection services)
    {
        services.AddOptions<BindDeployOptions>()
            .BindConfiguration(BindDeployOptions.SectionName);

        services.AddHttpClient<IBindDeployClient, BindDeployClient>()
            .ConfigureHttpClient((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptionsMonitor<BindDeployOptions>>().CurrentValue;

                // TryCreate rather than `new Uri(...)`: this callback runs during DI construction
                // of IBindDeployClient, which ReportDesignerController depends on for every one of
                // its actions — a malformed BaseUrl throwing here would 500 every endpoint on that
                // controller (extract-schema, match, consent, etc.), not just publish. Leaving
                // BaseAddress null instead means only BindDeployClient's own actual call fails,
                // with the clear "BaseAddress is null" message it already raises.
                if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
                {
                    if (Uri.TryCreate(opts.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri))
                    {
                        client.BaseAddress = baseUri;
                    }
                    else
                    {
                        sp.GetRequiredService<ILoggerFactory>()
                            .CreateLogger("BindDeployIntegration")
                            .LogError(
                                "BindDeploy:BaseUrl is not a valid absolute URI: '{BaseUrl}'. Did STBI_BIND_DEPLOY_BASE_URL omit the https:// scheme?",
                                opts.BaseUrl);
                    }
                }

                client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds > 0 ? opts.TimeoutSeconds : 60);

                if (!string.IsNullOrWhiteSpace(opts.ApiKey))
                    client.DefaultRequestHeaders.Add("X-Service-Api-Key", opts.ApiKey);
            });

        return services;
    }
}
