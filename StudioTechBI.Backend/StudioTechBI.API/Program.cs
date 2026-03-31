using Azure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using StudioTechBI.API.Authorization;
using StudioTechBI.API.Middleware;
using StudioTechBI.Application;
using StudioTechBI.Application.Models;
using StudioTechBI.Application.Services;
using StudioTechBI.Infrastructure;
using System.Text;
using System.Security.Claims;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Domain.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Local config: credentials and demo users (no database file required for demo)
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddJsonFile("DemoUsers.json", optional: true, reloadOnChange: true);

var envOverrides = new Dictionary<string, string?>();
MapEnvOverride("DB_CONNECTION", "ConnectionStrings:DefaultConnection");
MapEnvOverride("JWT_SECRET", "JwtSettings:SecretKey");
MapEnvOverride("JWT_ISSUER", "JwtSettings:Issuer");
MapEnvOverride("JWT_AUDIENCE", "JwtSettings:Audience");
MapEnvOverride("OPENAI_API_KEY", "OpenAI:ApiKey");
MapEnvOverride("POWERBI_CLIENT_ID", "PowerBI:ClientId");
MapEnvOverride("POWERBI_CLIENT_SECRET", "PowerBI:ClientSecret");
MapEnvOverride("POWERBI_TENANT_ID", "PowerBI:TenantId");
MapEnvOverride("POWERBI_WORKSPACE_ID", "PowerBI:WorkspaceId");
MapEnvOverride("POWERBI_REPORT_ID", "PowerBI:ReportId");
MapEnvOverride("POWERBI_DATASET_ID", "PowerBI:DatasetId");
MapEnvOverride("AZURE_AD_TENANT_ID", "AzureAd:TenantId");
MapEnvOverride("AZURE_AD_CLIENT_ID", "AzureAd:ClientId");
MapEnvOverride("AZURE_AD_CLIENT_SECRET", "AzureAd:ClientSecret");

if (envOverrides.Count > 0)
{
    builder.Configuration.AddInMemoryCollection(envOverrides);
}

var vaultUrl = builder.Configuration["KeyVault:VaultUrl"];

if(!string.IsNullOrWhiteSpace(vaultUrl))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(vaultUrl),
        new DefaultAzureCredential());
}

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient<AIInsightsService>();
builder.Services.AddScoped<AllInsightsService>();
builder.Services.AddScoped<ReportWorkerService>();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "StudioTechBI API",
        Version = "v1",
        Description = "SaaS Accounting Intelligence Platform API",
        Contact = new OpenApiContact
        {
            Name = "StudioTechBI",
            Email = "support@studiotechbi.com"
        }
    });

    c.AddServer(new OpenApiServer { Url = "https://localhost:5001", Description = "HTTPS" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var jwtSecretKey = builder.Configuration["JwtSettings:SecretKey"];
    if (string.IsNullOrWhiteSpace(jwtSecretKey))
    {
        throw new InvalidOperationException(
            "JwtSettings:SecretKey is required and must be non-empty. Provide it via environment variable 'JwtSettings__SecretKey'.");
    }

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
        ValidAudience = builder.Configuration["JwtSettings:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSecretKey))
    };
    options.Events = new JwtBearerEvents
    {
        OnChallenge = context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            var body = System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                message = "Missing or invalid token. Please log in again.",
                error = "Unauthorized"
            });
            return context.Response.WriteAsync(body);
        },
        OnTokenValidated = async context =>
        {
            var principal = context.Principal;
            if (principal?.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
            {
                context.Fail("Unauthenticated.");
                return;
            }

            var userIdRaw = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdRaw, out var userId))
                return;

            var cancellationToken = context.HttpContext.RequestAborted;
            using var scope = context.HttpContext.RequestServices.CreateScope();

            // Admin JWTs contain "AdminId"; normal user JWTs do not.
            var adminIdRaw = principal.FindFirstValue("AdminId");
            if (!string.IsNullOrWhiteSpace(adminIdRaw) && Guid.TryParse(adminIdRaw, out var adminId))
            {
                var adminRepo = scope.ServiceProvider.GetRequiredService<IRepository<AdminUser>>();
                var admin = await adminRepo.GetByIdAsync(adminId, cancellationToken);
                if (admin == null || admin.IsDeleted || !admin.IsActive)
                    context.Fail("Account inactive.");
                return;
            }

            var userRepo = scope.ServiceProvider.GetRequiredService<IRepository<User>>();
            var user = await userRepo.GetByIdAsync(userId, cancellationToken);
            if (user == null || user.IsDeleted || !user.IsActive)
                context.Fail("Account inactive.");
        }
    };
});

// Add client_code from DB when token lacks it (e.g. user assigned to client after login)
builder.Services.AddScoped<IClaimsTransformation, StudioTechBI.API.Authorization.ClientCodeClaimsTransformation>();

builder.Services.AddAuthorizationPolicies();

builder.Services.AddCors(options =>
{
    options.AddPolicy("RestrictedCors", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?.Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();

        if (!builder.Environment.IsDevelopment() && allowedOrigins.Length == 0)
        {
            throw new InvalidOperationException("Cors:AllowedOrigins must be configured for non-development environments.");
        }

        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
    });
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<PowerBISettings>(
    builder.Configuration.GetSection("PowerBI"));

builder.Services.AddScoped<PowerBIService>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<StudioTechBI.Infrastructure.Data.ApplicationDbContext>(
        name: "sql",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "db", "ready" });

var app = builder.Build();

var enableSwaggerInAzure = app.Configuration.GetValue<bool>("Swagger:Enabled");
if (app.Environment.IsDevelopment() || enableSwaggerInAzure)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseHttpsRedirection();

app.UseCors("RestrictedCors");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

ValidateRequiredSettings(app.Configuration, app.Environment);

var useDemoStorage = app.Configuration.GetValue<bool>("UseDemoStorage");
var connectionString = app.Configuration.GetConnectionString("DefaultConnection");
if (useDemoStorage)
{
    Log.Warning("Database mode is set to in-memory demo storage.");
} else
{
    Log.Information("Database mode is set to SQL Server.");
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StudioTechBI.Infrastructure.Data.ApplicationDbContext>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    try
    {
        if (useDemoStorage)
        {
            await StudioTechBI.Infrastructure.Data.RoleSeeder.SeedAsync(db);
            await StudioTechBI.Infrastructure.Data.DemoUsersSeeder.SeedAsync(db, config);
            await StudioTechBI.Infrastructure.Data.AdminUserSeeder.SeedAsync(db, config);
            Log.Information("Demo storage ready. Roles, users, and admin seeded.");
        }
        else
        {
            try
            {
                if (!await db.Database.CanConnectAsync())
                {
                    Log.Error("Cannot connect to database.");
                    throw new InvalidOperationException("Database connection failed.");
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Database connection failed.");
                throw new InvalidOperationException("Database connection failed.", ex);
            }
            Log.Information("DB connection established.");
            Log.Information("Applying EF Core migrations...");
            await db.Database.MigrateAsync();
            Log.Information("Migrations applied.");
            await StudioTechBI.Infrastructure.Data.RoleSeeder.SeedAsync(db);
            await StudioTechBI.Infrastructure.Data.AdminUserSeeder.SeedAsync(db, config);
            Log.Information("Database migrations applied. Roles and admin user seeded.");
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Database setup or seed failed.");
        throw;
    }
}

void MapEnvOverride(string environmentVariableName, string configPath)
{
    var value = Environment.GetEnvironmentVariable(environmentVariableName);
    if (!string.IsNullOrWhiteSpace(value))
    {
        envOverrides[configPath] = value;
    }
}

static void ValidateRequiredSettings(IConfiguration configuration, IWebHostEnvironment environment)
{
    var enforceInDev = configuration.GetValue<bool>("Startup:EnforceProductionConfigValidation");

    if (environment.IsDevelopment() && !enforceInDev)
    {
        return;
    }

    var useDemoStorage = configuration.GetValue<bool>("UseDemoStorage");
    var requiredKeys = new List<string>
    {
        "JwtSettings:SecretKey",
        "JwtSettings:Issuer",
        "JwtSettings:Audience",
        "Cors:AllowedOrigins:0"
    };

    if (!environment.IsDevelopment() || !useDemoStorage)
    {
        requiredKeys.Insert(0, "ConnectionStrings:DefaultConnection");
    }

    if (environment.IsDevelopment() && enforceInDev)
    {
        Log.Information(
            "Startup:EnforceProductionConfigValidation=true: validating {Count} config keys (DB connection {DbMode}).",
            requiredKeys.Count,
            useDemoStorage ? "skipped (UseDemoStorage=true)" : "required");
    }

    foreach (var key in requiredKeys)
    {
        if (string.IsNullOrWhiteSpace(configuration[key]))
        {
            throw new InvalidOperationException($"Missing required configuration key: {key}");
        }
    }
}

try
{
    Log.Information("Starting StudioTechBI API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
}
finally
{
    Log.CloseAndFlush();
}
