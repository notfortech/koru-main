using Microsoft.Extensions.DependencyInjection;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Services;

namespace StudioTechBI.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<DataSamplingService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAdminAuthService, AdminAuthService>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<ITemplateService, TemplateService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IAdminMaintenanceService, AdminMaintenanceService>();
        services.AddScoped<IInsightService, InsightService>();
        services.AddScoped<InsightSelectionPipeline>();
        services.AddScoped<IInsightReportService, InsightReportService>();
        services.AddScoped<IDataConnectionService, DataConnectionService>();
        services.AddScoped<IClientResolver, ClientResolver>();
        services.AddScoped<IOAuthService, OAuthService>();
        services.AddScoped<IConnectionService, ConnectionService>();
        services.AddScoped<IInsightTemplateVerificationService, InsightTemplateVerificationService>();
        services.AddScoped<ITemplateMatchingService, TemplateMatchingService>();
        services.AddScoped<IReportDesignerConsentService, ReportDesignerConsentService>();

        return services;
    }
}
