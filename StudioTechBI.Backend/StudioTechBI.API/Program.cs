using Azure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using StudioTechBI.Infrastructure.Extensions;
using StudioTechBI.API.Authorization;
using StudioTechBI.API.Configuration;
using StudioTechBI.API.Integrations.Common;
using StudioTechBI.API.Services.Domain;
using StudioTechBI.API.Middleware;
using StudioTechBI.Application;
using StudioTechBI.Application.Models;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Services;
using StudioTechBI.Infrastructure;
using System.Text;
using System.Security.Claims;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Domain.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Azure App Service (Linux) listens on the PORT environment variable.
// Bind explicitly so warmup/health probes can reach Kestrel.
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

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
MapEnvOverride("INSIGHT_ENGINE_ENABLED", "InsightEngine:Enabled");
MapEnvOverride("INSIGHT_ENGINE_BASE_URL", "InsightEngine:BaseUrl");
MapEnvOverride("INSIGHT_ENGINE_API_KEY", "InsightEngine:ApiKey");
// Transformations suggest API (InsightsEngine.Api) — different section from InsightEngine (model generation).
MapEnvOverride("INSIGHTS_ENGINE_BASE_URL", "InsightsEngine:BaseUrl");
MapEnvOverride("INSIGHTS_ENGINE_API_KEY", "InsightsEngine:ApiKey");
MapEnvOverride("INSIGHTS_ENGINE_EXTERNAL_COPILOT_AI", "InsightsEngine:ExternalCopilotAiEnabled");
MapEnvOverride("INSIGHTS_ENGINE_MIN_MATCH_SCORE_SKIP_AI", "InsightsEngine:MinTemplateMatchScoreToSkipExternalAi");
MapEnvOverride("REPORT_INSIGHTS_DATASET_EXECUTE_ENABLED", "ReportInsights:DatasetExecuteQueriesEnabled");
MapEnvOverride("MICROSOFT_AUTH_CLIENT_ID", "MicrosoftAuth:ClientId");
MapEnvOverride("MICROSOFT_AUTH_CLIENT_SECRET", "MicrosoftAuth:ClientSecret");
MapEnvOverride("MICROSOFT_AUTH_TENANT_ID", "MicrosoftAuth:TenantId");
MapEnvOverride("MICROSOFT_AUTH_REDIRECT_URI", "MicrosoftAuth:RedirectUri");
MapEnvOverride("MICROSOFT_AUTH_SUCCESS_REDIRECT_URI", "MicrosoftAuth:SuccessRedirectUri");
MapEnvOverride("GOOGLE_AUTH_CLIENT_ID", "GoogleAuth:ClientId");
MapEnvOverride("GOOGLE_AUTH_CLIENT_SECRET", "GoogleAuth:ClientSecret");
MapEnvOverride("GOOGLE_AUTH_REDIRECT_URI", "GoogleAuth:RedirectUri");
MapEnvOverride("GOOGLE_AUTH_SUCCESS_REDIRECT_URI", "GoogleAuth:SuccessRedirectUri");
MapEnvOverride("AGENT_HOST_BASE_URL", "AgentHost:BaseUrl");
MapEnvOverride("AGENT_HOST_API_KEY", "AgentHost:ApiKey");
MapEnvOverride("AGENT_HOST_TIMEOUT_SECONDS", "AgentHost:TimeoutSeconds");
MapEnvOverride("AGENT_HOST_RETRY_COUNT", "AgentHost:RetryCount");
MapEnvOverride("REPORT_DESIGNER_BASE_URL", "ReportDesigner:BaseUrl");
MapEnvOverride("TRANSFORMERS_AGENTS_BASEURL", "ReportDesigner:BaseUrl");  // canonical env var for stbi_transformers
MapEnvOverride("REPORT_DESIGNER_API_KEY", "ReportDesigner:ApiKey");
MapEnvOverride("TRANSFORMERS_AGENTS_API_KEY", "ReportDesigner:ApiKey");  // canonical env var for stbi_transformers
MapEnvOverride("REPORT_DESIGNER_TIMEOUT_SECONDS", "ReportDesigner:TimeoutSeconds");
MapEnvOverride("REPORT_GENERATOR_BASE_URL", "ReportGenerator:BaseUrl");
MapEnvOverride("REPORT_GENERATOR_API_KEY", "ReportGenerator:ApiKey");
MapEnvOverride("REPORT_GENERATOR_TIMEOUT_SECONDS", "ReportGenerator:TimeoutSeconds");
MapEnvOverride("AGENT_HOST_ENABLE_CIRCUIT_BREAKER", "AgentHost:EnableCircuitBreaker");
MapEnvOverride("AGENT_HOST_CIRCUIT_FAILURE_THRESHOLD", "AgentHost:CircuitFailureThreshold");
MapEnvOverride("AGENT_HOST_CIRCUIT_BREAK_DURATION", "AgentHost:CircuitBreakDurationSeconds");
MapEnvOverride("AGENT_HOST_HEALTH_CHECK_PATH", "AgentHost:HealthCheckPath");
MapEnvOverride("DOMAIN_API_CLIENT_ID", "DomainApi:ClientId");
MapEnvOverride("DOMAIN_API_CLIENT_SECRET", "DomainApi:ClientSecret");
MapEnvOverride("DOMAIN_API_BASE_URL", "DomainApi:BaseUrl");
MapEnvOverride("DOMAIN_API_AUTH_BASE_URL", "DomainApi:AuthBaseUrl");
MapEnvOverride("DOMAIN_API_SCOPE", "DomainApi:Scope");
// Shared secret validating inbound calls to POST /api/internal/ai-boundary-audit-log (S5).
// Must be set to the same value as stbi_transformers' KORU_API_KEY (Koru:ServiceApiKey there) —
// that's the credential it already sends as X-Service-Api-Key on every call to this backend.
MapEnvOverride("STBI_TRANSFORMERS_API_KEY", "Security:StbiTransformersApiKey");
MapEnvOverride("STBI_BIND_DEPLOY_BASE_URL", "BindDeploy:BaseUrl");
MapEnvOverride("STBI_BIND_DEPLOY_API_KEY", "BindDeploy:ApiKey");

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
builder.Services.AddMemoryCache();
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

    // "Try it out" uses OpenAPI `servers` — do NOT hardcode localhost or Azure will still show localhost
    // when the spec is downloaded. Use same-origin, or set Swagger:OpenApiServerUrl (e.g. public API URL).
    {
        var serverUrl = (builder.Configuration["Swagger:OpenApiServerUrl"] ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(serverUrl))
        {
            c.AddServer(new OpenApiServer
            {
                Url = "/",
                Description = "Same host as this Swagger page (local or Azure)"
            });
        }
        else
        {
            c.AddServer(new OpenApiServer
            {
                Url = serverUrl.TrimEnd('/'),
                Description = "Swagger:OpenApiServerUrl"
            });
        }
    }

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
    // Azure warmup + browser preflight should never block startup.
    // For production, tighten origins once the app is stable.
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });

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
builder.Services.AddAgentHostIntegration();
builder.Services.AddReportDesignerIntegration();
builder.Services.AddReportGeneratorIntegration();
builder.Services.AddBindDeployIntegration();
builder.Services.Configure<PasswordResetOptions>(
    builder.Configuration.GetSection(PasswordResetOptions.SectionName));
builder.Services.Configure<InsightEngineOptions>(
    builder.Configuration.GetSection(InsightEngineOptions.SectionName));
builder.Services.Configure<PowerBISettings>(
    builder.Configuration.GetSection("PowerBI"));
builder.Services.Configure<ReportInsightsOptions>(
    builder.Configuration.GetSection(ReportInsightsOptions.SectionName));
builder.Services.Configure<MicrosoftAuthOptions>(
    builder.Configuration.GetSection(MicrosoftAuthOptions.SectionName));
builder.Services.Configure<DomainApiSettings>(
    builder.Configuration.GetSection(DomainApiSettings.SectionName));

builder.Services.AddHttpClient(DomainHttpClientNames.Auth, (sp, client) =>
{
    var s = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DomainApiSettings>>().Value;
    var baseUrl = (string.IsNullOrWhiteSpace(s.AuthBaseUrl) ? "https://auth.domain.com.au" : s.AuthBaseUrl).TrimEnd('/');
    client.BaseAddress = new Uri(baseUrl + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddSingleton<DomainAccessTokenProvider>();
builder.Services.AddSingleton<IIntegrationAccessTokenProvider>(sp => sp.GetRequiredService<DomainAccessTokenProvider>());

builder.Services.AddHttpClient<IDomainPropertyLocationService, DomainPropertyLocationService>((sp, client) =>
    {
        var s = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DomainApiSettings>>().Value;
        var baseUrl = (string.IsNullOrWhiteSpace(s.BaseUrl) ? "https://api.domain.com.au" : s.BaseUrl).TrimEnd('/');
        client.BaseAddress = new Uri(baseUrl + "/");
        client.Timeout = TimeSpan.FromSeconds(60);
    })
    .AddPolicyHandler(DomainResourceApiRetryPolicy());

// Google OAuth: Azure App Settings / env as GOOGLE_AUTH_* first, then GoogleAuth:* (appsettings, Key Vault, MapEnvOverride).
builder.Services.Configure<GoogleAuthOptions>(options =>
{
    var cfg = builder.Configuration;
    options.ClientId = cfg["GOOGLE_AUTH_CLIENT_ID"] ?? cfg["GoogleAuth:ClientId"] ?? string.Empty;
    options.ClientSecret = cfg["GOOGLE_AUTH_CLIENT_SECRET"] ?? cfg["GoogleAuth:ClientSecret"] ?? string.Empty;
    options.RedirectUri = cfg["GOOGLE_AUTH_REDIRECT_URI"] ?? cfg["GoogleAuth:RedirectUri"] ?? string.Empty;
    options.SuccessRedirectUri = cfg["GOOGLE_AUTH_SUCCESS_REDIRECT_URI"] ?? cfg["GoogleAuth:SuccessRedirectUri"];
});

builder.Services.AddScoped<PowerBIService>();
builder.Services.AddScoped<IPowerBiDatasetSampleService, PowerBiDatasetSampleService>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<StudioTechBI.Infrastructure.Data.ApplicationDbContext>(
        name: "sql",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "db", "ready" });

// Gate /api/* until background migrations finish (avoids 500 when DB schema is not ready yet).
builder.Services.AddSingleton<StudioTechBI.API.Services.DatabaseReadinessState>();

// Do not block startup on DB work; run migrations/seeding after the app is listening.
builder.Services.AddHostedService<StudioTechBI.API.Services.StartupDbTasksHostedService>();

var app = builder.Build();

// Startup-time config visibility for the newer integrations — BlueprintGenerationBackgroundService
// already logs its own BaseUrl on start; ReportDesigner/BindDeploy's HttpClients are lazy (built on
// first use via IHttpClientFactory) and previously gave no signal at boot if BaseUrl/ApiKey were
// missing, only a failure on the first real call. Logging the resolved values (never the key itself)
// right after DI is built closes that gap.
{
    var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
    var reportDesignerOpts = app.Services.GetRequiredService<IOptionsMonitor<ReportDesignerOptions>>().CurrentValue;
    var bindDeployOpts = app.Services.GetRequiredService<IOptionsMonitor<BindDeployOptions>>().CurrentValue;
    startupLogger.LogInformation(
        "ReportDesigner integration configured. BaseUrl={BaseUrl} ApiKeySet={ApiKeySet} TimeoutSeconds={TimeoutSeconds}",
        string.IsNullOrWhiteSpace(reportDesignerOpts.BaseUrl) ? "(not set)" : reportDesignerOpts.BaseUrl,
        !string.IsNullOrWhiteSpace(reportDesignerOpts.ApiKey),
        reportDesignerOpts.TimeoutSeconds);
    startupLogger.LogInformation(
        "BindDeploy integration configured. BaseUrl={BaseUrl} ApiKeySet={ApiKeySet} TimeoutSeconds={TimeoutSeconds}",
        string.IsNullOrWhiteSpace(bindDeployOpts.BaseUrl) ? "(not set)" : bindDeployOpts.BaseUrl,
        !string.IsNullOrWhiteSpace(bindDeployOpts.ApiKey),
        bindDeployOpts.TimeoutSeconds);
}

var enableSwaggerInAzure = app.Configuration.GetValue<bool>("Swagger:Enabled");
if (app.Environment.IsDevelopment() || enableSwaggerInAzure)
{
    app.UseSwagger();
    app.UseSwaggerUI();
    // Root URL has no controller; send browsers to Swagger (launchUrl is often just the base URL).
    app.MapGet("/", () => Results.Redirect("/swagger")).AllowAnonymous().ExcludeFromDescription();
}
else
{
    // Azure warmup endpoint: must return immediately.
    app.MapGet("/", () => Results.Ok("API is running")).AllowAnonymous().ExcludeFromDescription();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseHttpsRedirection();

app.UseCors("AllowAll");
app.UseMiddleware<StudioTechBI.API.Middleware.DatabaseReadinessMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

ValidateRequiredSettings(app.Configuration, app.Environment);

void MapEnvOverride(string environmentVariableName, string configPath)
{
    var value = Environment.GetEnvironmentVariable(environmentVariableName);
    if (!string.IsNullOrWhiteSpace(value))
    {
        envOverrides[configPath] = value;
    }
}

static IAsyncPolicy<HttpResponseMessage> DomainResourceApiRetryPolicy() =>
    HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(
            3,
            retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 250)));

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
        // Keep this required key for existing deployments that still use RestrictedCors.
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
