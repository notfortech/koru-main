using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.API.Services;

/// <summary>
/// Runs database migrations and seeding after the app is already listening.
/// This keeps Azure warmup probes fast and avoids ContainerTimeout crash loops.
/// </summary>
public sealed class StartupDbTasksHostedService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StartupDbTasksHostedService> _logger;

    public StartupDbTasksHostedService(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger<StartupDbTasksHostedService> logger)
    {
        _services = services;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small delay to allow Kestrel to bind and Azure warmup to succeed.
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        var useDemoStorage = _configuration.GetValue<bool>("UseDemoStorage");
        if (useDemoStorage)
        {
            _logger.LogWarning("UseDemoStorage=true: seeding demo data in background.");
        }
        else
        {
            _logger.LogInformation("SQL Server mode: applying migrations in background.");
        }

        try
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            if (useDemoStorage)
            {
                await RoleSeeder.SeedAsync(db);
                await DemoUsersSeeder.SeedAsync(db, _configuration);
                await AdminUserSeeder.SeedAsync(db, _configuration);
                _logger.LogInformation("Demo storage ready. Roles/users/admin seeded.");
                return;
            }

            // Don't throw if DB is momentarily unavailable during container start; retry a few times.
            for (var attempt = 1; attempt <= 5 && !stoppingToken.IsCancellationRequested; attempt++)
            {
                try
                {
                    if (!await db.Database.CanConnectAsync(stoppingToken))
                        throw new InvalidOperationException("Database connection failed.");

                    _logger.LogInformation("DB connection established. Applying EF Core migrations...");
                    await db.Database.MigrateAsync(stoppingToken);
                    _logger.LogInformation("Migrations applied. Seeding roles/admin...");
                    await RoleSeeder.SeedAsync(db);
                    await AdminUserSeeder.SeedAsync(db, _configuration);
                    _logger.LogInformation("Database ready (migrations + seed complete).");
                    return;
                }
                catch (Exception ex) when (attempt < 5)
                {
                    _logger.LogWarning(ex, "DB init attempt {Attempt}/5 failed; retrying shortly.", attempt);
                    await Task.Delay(TimeSpan.FromSeconds(5 * attempt), stoppingToken);
                }
            }

            _logger.LogError("DB init failed after retries. App will keep running, but DB-backed endpoints may fail.");
        }
        catch (Exception ex)
        {
            // Critical: never crash the web host during warmup.
            _logger.LogError(ex, "Startup DB tasks failed (non-fatal).");
        }
    }
}

