using Microsoft.Extensions.DependencyInjection;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Services;

namespace StudioTechBI.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAdminAuthService, AdminAuthService>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<ITemplateService, TemplateService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IAdminMaintenanceService, AdminMaintenanceService>();
        services.AddScoped<IInsightService, InsightService>();
        services.AddScoped<IDataConnectionService, DataConnectionService>();

        return services;
    }
}
