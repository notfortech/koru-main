using Microsoft.Extensions.DependencyInjection;
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

                if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
                    client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");

                client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds > 0 ? opts.TimeoutSeconds : 60);

                if (!string.IsNullOrWhiteSpace(opts.ApiKey))
                    client.DefaultRequestHeaders.Add("X-Service-Api-Key", opts.ApiKey);
            });

        return services;
    }
}
