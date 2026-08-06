using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly.Extensions.Http;
using Polly;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;
using StudioTechBI.Application.Options;
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
        services.AddScoped<IAiBoundaryAuditService, AiBoundaryAuditService>();
        services.AddOptions<SecurityOptions>().BindConfiguration(SecurityOptions.SectionName);
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IAdminDashboardService, AdminDashboardService>();
        services.AddScoped<IClientByCompanyQuery, ClientByCompanyQuery>();
        services.AddScoped<IDatasetRefreshLogWriter, DatasetRefreshLogWriter>();
        services.AddScoped<IReportingProcessingJobWriter, ReportingProcessingJobWriter>();
        services.AddScoped<IReportingTechnicalLogWriter, ReportingTechnicalLogWriter>();
        services.AddScoped<IPowerBiAssetQuery, PowerBiAssetQuery>();
        services.AddScoped<IPowerBiAssetWriter, PowerBiAssetWriter>();
        services.AddScoped<IClientPortalDashboardService, ClientPortalDashboardService>();
        services.AddScoped<IReportTemplateAssetService, ReportTemplateAssetService>();
        services.AddScoped<IWorkbookWriter, WorkbookWriter>();
        services.AddScoped<IBlobSasUriProvider, BlobSasUriProvider>();
        services.AddScoped<IDashboardTemplateLogWriter, DashboardTemplateLogWriter>();
        services.AddScoped<IHtmlReportAssemblyService, HtmlReportAssemblyService>();
        services.AddScoped<ILocalCreditLedgerService, LocalCreditLedgerService>();
        services.AddScoped<IHtmlTemplateSyncRunner, HtmlTemplateSyncRunner>();
        services.AddScoped<IHtmlTemplateAdminService, HtmlTemplateAdminService>();
        services.AddHostedService<HtmlTemplateRegistrySyncService>();

        services.Configure<UploadLimitsOptions>(configuration.GetSection(UploadLimitsOptions.SectionName));

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

        // External InsightsEngine transformations suggestion API (separate from InsightEngine models/orchestration API).
        services.Configure<InsightsEngineOptions>(configuration.GetSection(InsightsEngineOptions.SectionName));
        services.AddHttpClient<InsightsEngineClient>()
            .ConfigureHttpClient((sp, client) =>
            {
                var o = sp.GetRequiredService<IOptionsMonitor<InsightsEngineOptions>>().CurrentValue;
                // Back-compat: allow using InsightEngine:* settings if InsightsEngine:* is not set.
                var baseUrl = (o.BaseUrl ?? "").Trim();
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    var legacy = sp.GetRequiredService<IOptionsMonitor<InsightEngineOptions>>().CurrentValue.BaseUrl;
                    baseUrl = (legacy ?? "").Trim();
                }
                if (!string.IsNullOrWhiteSpace(baseUrl))
                    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");

                client.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds > 0 ? o.TimeoutSeconds : 120);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                client.DefaultRequestHeaders.Remove("X-Api-Key");
                var key = (o.ApiKey ?? "").Trim();
                if (string.IsNullOrEmpty(key))
                {
                    var legacyKey = sp.GetRequiredService<IOptionsMonitor<InsightEngineOptions>>().CurrentValue.ApiKey;
                    key = (legacyKey ?? "").Trim();
                }
                if (!string.IsNullOrEmpty(key))
                    client.DefaultRequestHeaders.Add("X-Api-Key", key);
            })
            .AddPolicyHandler(
                HttpPolicyExtensions
                    .HandleTransientHttpError() // 5xx, 408, network
                    .OrResult(r => (int)r.StatusCode == 429)
                    .WaitAndRetryAsync(
                        retryCount: 3,
                        sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))));
        services.AddScoped<IInsightsEngineTemplateMappingClient>(sp => sp.GetRequiredService<InsightsEngineClient>());
        services.AddScoped<IInsightsEngineReportInsightsClient>(sp => sp.GetRequiredService<InsightsEngineClient>());
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
