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
    private readonly DatabaseReadinessState _readiness;

    public StartupDbTasksHostedService(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger<StartupDbTasksHostedService> logger,
        DatabaseReadinessState readiness)
    {
        _services = services;
        _configuration = configuration;
        _logger = logger;
        _readiness = readiness;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Kestrel is already listening; DB gate blocks /api until we finish.
        await Task.Yield();

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
                await SeedSchemaModelsNonFatalAsync(db, stoppingToken);
                _readiness.MarkDatabaseReady();
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

                    // Everything above this line is required for core login/API access and stays
                    // inside the retry loop. SchemaModel seeding is not on that critical path —
                    // it must never be able to block readiness, so it gets its own fault boundary.
                    // Also runs the hand-authored migrations' guarded DDL directly (belt-and-braces
                    // — see HandwrittenMigrationsBootstrapper / MIGRATIONS.md for why).
                    await HandwrittenMigrationsBootstrapper.EnsureTablesExistAsync(db, _logger, stoppingToken);
                    await SeedSchemaModelsNonFatalAsync(db, stoppingToken);

                    _readiness.MarkDatabaseReady();
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

    /// <summary>
    /// SchemaModel reference-data seeding must never be able to block app readiness — a failure
    /// here previously propagated up through the retry loop and prevented
    /// _readiness.MarkDatabaseReady() from ever being called, which took down login entirely
    /// (every /api/* call gated on readiness returned 503). See MIGRATIONS.md.
    /// </summary>
    private async Task SeedSchemaModelsNonFatalAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            await SchemaModelSeeder.SeedAsync(db, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SchemaModel seeding failed; continuing without it. Core app readiness is unaffected.");
        }
    }
}

