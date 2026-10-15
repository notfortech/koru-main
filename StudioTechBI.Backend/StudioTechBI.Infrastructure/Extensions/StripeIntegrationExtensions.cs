using Microsoft.Extensions.DependencyInjection;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;
using StudioTechBI.Infrastructure.Clients;
using StudioTechBI.Infrastructure.Services;

namespace StudioTechBI.Infrastructure.Extensions;

public static class StripeIntegrationExtensions
{
    public static IServiceCollection AddStripeIntegration(this IServiceCollection services)
    {
        services.AddOptions<StripeOptions>()
            .BindConfiguration(StripeOptions.SectionName);

        services.AddScoped<IStripeCheckoutService, StripeCheckoutService>();
        services.AddScoped<ICreditPurchaseFulfillmentService, CreditPurchaseFulfillmentService>();

        return services;
    }
}
