using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;
using StudioTechBI.Infrastructure.Clients;

namespace StudioTechBI.Infrastructure.Extensions;

/// <summary>
/// New, standalone registration for ITemplateRebindClient. Reuses BindDeployOptions unmodified —
/// the rebind endpoint lives on the same stbi-bind-deploy service, same base URL and API key —
/// rather than adding a method (and a new option) to BindDeployIntegrationExtensions.
/// </summary>
public static class TemplateRebindIntegrationExtensions
{
    public static IServiceCollection AddTemplateRebindIntegration(this IServiceCollection services)
    {
        services.AddOptions<BindDeployOptions>()
            .BindConfiguration(BindDeployOptions.SectionName);
        services.AddOptions<TemplateCatalogOptions>()
            .BindConfiguration(TemplateCatalogOptions.SectionName);

        services.AddHttpClient<ITemplateRebindClient, TemplateRebindClient>()
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
                            .CreateLogger("TemplateRebindIntegration")
                            .LogError(
                                "BindDeploy:BaseUrl is not a valid absolute URI: '{BaseUrl}'.",
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
