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

var builder = WebApplication.CreateBuilder(args);

// Local config: credentials and demo users (no database file required for demo)
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddJsonFile("DemoUsers.json", optional: true, reloadOnChange: true);

var tenantId = builder.Configuration["AzureAd:TenantId"];
var clientId = builder.Configuration["AzureAd:ClientId"];
var clientSecret = builder.Configuration["AzureAd:ClientSecret"];
var vaultUrl = builder.Configuration["KeyVault:VaultUrl"];

var credential = new ClientSecretCredential(
    tenantId,
    clientId,
    clientSecret);

if(!string.IsNullOrEmpty(vaultUrl))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(vaultUrl),
        credential);
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
builder.Services.AddScoped<AIInsightsService>();
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

    // Default to HTTP so Swagger works without trusting the dev HTTPS certificate; use HTTPS when cert is trusted
    c.AddServer(new OpenApiServer { Url = "http://localhost:5000", Description = "HTTP (use this if HTTPS fails)" });
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
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
        ValidAudience = builder.Configuration["JwtSettings:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:SecretKey"] ?? throw new InvalidOperationException("JWT Secret Key not configured")))
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
                error = context.AuthenticateFailure?.Message ?? "Unauthorized"
            });
            return context.Response.WriteAsync(body);
        }
    };
});

// Add client_code from DB when token lacks it (e.g. user assigned to client after login)
builder.Services.AddScoped<IClaimsTransformation, StudioTechBI.API.Authorization.ClientCodeClaimsTransformation>();

builder.Services.AddAuthorizationPolicies();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
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

// Swagger in Development and Staging only (not Production)
if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", $"StudioTechBI API v1 ({app.Environment.EnvironmentName})");
        c.RoutePrefix = string.Empty;
    });
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

// Skip HTTPS redirect in Development so POST from Swagger over HTTP works (no 307)
if (app.Environment.IsProduction())
    app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// Config order (later overrides earlier): appsettings.json → appsettings.{Environment}.json → appsettings.Local.json.
// So appsettings.Local.json wins when present. Put Azure SQL connection string in appsettings.Local.json (gitignored) so secrets are not committed.
var useDemoStorage = app.Configuration.GetValue<bool>("UseDemoStorage");
var connectionString = app.Configuration.GetConnectionString("DefaultConnection");
var serverForLog = connectionString != null && connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase)
    ? System.Text.RegularExpressions.Regex.Match(connectionString, @"Server=([^;]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Groups[1].Value
    : (connectionString != null && connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
        ? System.Text.RegularExpressions.Regex.Match(connectionString, @"Data Source=([^;]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Groups[1].Value
        : null);
Log.Information("connectionString: " + connectionString);
if (useDemoStorage)
{
    Log.Information("Database: In-memory (demo). UseDemoStorage=true. Set UseDemoStorage=false in appsettings.Local.json to use Azure SQL.");
} else
    Log.Information("Database: SQL Server. UseDemoStorage=false. Connection server: {Server}. (From appsettings.Local.json if that file exists, else appsettings.json.)", serverForLog ?? "(unknown)");

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
            await StudioTechBI.Infrastructure.Data.AdminUserSeeder.SeedAsync(db);
            Log.Information("Demo storage ready. Roles, users, and admin seeded.");
        }
        else
        {
            try
            {
                if (!await db.Database.CanConnectAsync())
                {
                    Log.Error("Cannot connect to database. Check: (1) ConnectionStrings:DefaultConnection in appsettings.Local.json (correct server, database, user, password); (2) Azure SQL firewall allows your IP; (3) Password has no curly braces {{ }} around it.");
                    throw new InvalidOperationException("Database connection failed. Verify Azure SQL connection string and network access.");
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.Message ?? ex.Message;
                Log.Error("Database connection failed. Azure/SQL error: {InnerMessage}. Check connection string, password (no {{ }} in password), and firewall.", inner);
                throw new InvalidOperationException($"Database connection failed: {inner}", ex);
            }
            Log.Information("DB connection established.");
            Log.Information("Applying EF Core migrations...");
            await db.Database.MigrateAsync();
            Log.Information("Migrations applied.");
            await StudioTechBI.Infrastructure.Data.RoleSeeder.SeedAsync(db);
            await StudioTechBI.Infrastructure.Data.AdminUserSeeder.SeedAsync(db);
            Log.Information("Database migrations applied. Roles and admin user seeded.");
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Database setup or seed failed: {Message}", ex.Message);
        throw;
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
