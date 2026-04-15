using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;
using StudioTechBI.Domain.Interfaces;
using StudioTechBI.Infrastructure.Clients;
using StudioTechBI.Infrastructure.Connectors;
using StudioTechBI.Infrastructure.Data;
using StudioTechBI.Infrastructure.Repositories;
using StudioTechBI.Infrastructure.Services;

namespace StudioTechBI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var useDemoStorage = configuration.GetValue<bool>("UseDemoStorage");
        var connectionString = configuration["DB_CONNECTION"] ?? configuration.GetConnectionString("DefaultConnection");
        var migrationsAssembly = typeof(ApplicationDbContext).Assembly.FullName!;

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (useDemoStorage)
            {
                options.UseInMemoryDatabase("StudioTechBI_Demo");
            }
            else
            {
                var conn = connectionString ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection required when UseDemoStorage is false (e.g. Azure SQL).");
                options.UseSqlServer(conn, b =>
                {
                    b.MigrationsAssembly(migrationsAssembly);
                    b.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
                });
            }
        });

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IBlobStorageService, BlobStorageService>();
        services.AddScoped<IProcessingJobService, ProcessingJobService>();
        services.AddScoped<IAdminLoggingService, AdminLoggingService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IAdminDashboardService, AdminDashboardService>();
        services.AddScoped<IClientByCompanyQuery, ClientByCompanyQuery>();
        services.AddScoped<IDatasetRefreshLogWriter, DatasetRefreshLogWriter>();
        services.AddScoped<IReportingProcessingJobWriter, ReportingProcessingJobWriter>();
        services.AddScoped<IReportingTechnicalLogWriter, ReportingTechnicalLogWriter>();
        services.AddScoped<IPowerBiAssetQuery, PowerBiAssetQuery>();
        services.AddScoped<IClientPortalDashboardService, ClientPortalDashboardService>();

        services.Configure<InsightEngineOptions>(configuration.GetSection(InsightEngineOptions.SectionName));
        services.AddHttpClient<InsightEngineClient>()
            .ConfigureHttpClient((sp, client) =>
            {
                var o = sp.GetRequiredService<IOptionsMonitor<InsightEngineOptions>>().CurrentValue;
                client.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds > 0 ? o.TimeoutSeconds : 120);
                if (!string.IsNullOrWhiteSpace(o.BaseUrl))
                    client.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/");
            });
        services.AddScoped<IInsightEngineClient>(sp =>
        {
            var o = sp.GetRequiredService<IOptions<InsightEngineOptions>>().Value;
            if (!o.Enabled)
                return new DisabledInsightEngineClient();
            return sp.GetRequiredService<InsightEngineClient>();
        });
        services.AddScoped<IModelRepository, ModelRepository>();
        services.AddScoped<IDatasetRepository, DatasetRepository>();
        services.AddScoped<IDataConnectionRepository, DataConnectionRepository>();
        services.AddSingleton<MicrosoftGraphClientFactory>();
        services.AddScoped<GoogleDriveConnector>();
        services.AddScoped<OneDriveConnector>();
        services.AddScoped<SharePointConnector>();
        services.AddScoped<IDataConnectorRegistry, DataConnectorRegistry>();

        return services;
    }
}
