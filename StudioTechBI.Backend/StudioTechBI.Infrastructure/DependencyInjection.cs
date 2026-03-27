using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Interfaces;
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

        return services;
    }
}
