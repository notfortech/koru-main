using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.DTOs.Blueprints;
using StudioTechBI.Application.DTOs.Credits;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;

namespace StudioTechBI.Infrastructure.Clients;

/// <summary>
/// Typed HTTP client for STBI-AgentHost.
/// Authentication (API key or Bearer), retry, and circuit-breaker policies
/// are configured via IHttpClientFactory in AgentHostIntegrationExtensions.
/// </summary>
public class AgentHostClient : IAgentHostClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<AgentHostClient> _logger;
    private readonly AgentHostOptions _opts;
    private readonly CreditsOptions _creditsOpts;

    public AgentHostClient(
        HttpClient httpClient,
        ILogger<AgentHostClient> logger,
        IOptions<AgentHostOptions> options,
        IOptions<CreditsOptions> creditsOptions)
    {
        _httpClient = httpClient;
        _logger = logger;
        _opts = options.Value;
        _creditsOpts = creditsOptions.Value;
    }

    public async Task<BlueprintGenerationResponse> GenerateBlueprintAsync(
    GenerateBlueprintRequest request,
    string correlationId,
    CancellationToken cancellationToken = default)
{
    if (request.TenantId is null || request.TenantId == Guid.Empty)
    {
        throw new InvalidOperationException(
            "GenerateBlueprintAsync requires a resolved TenantId. " +
            "This should already be set by BlueprintsController before the request is queued.");
    }

    var payload = JsonSerializer.Serialize(
        AgentHostBlueprintRequest.From(request),
        JsonOptions);

    var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/blueprints/generate");
    httpRequest.Headers.Add("X-Correlation-Id", correlationId);
    httpRequest.Headers.Add("X-Tenant-Id", request.TenantId.Value.ToString());
    httpRequest.Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

    _logger.LogInformation(
        "AgentHost Diagnostic: BaseUrl={BaseUrl}, HasAuthorization={HasAuthorization}, Scheme={Scheme}, HasXApiKey={HasXApiKey}, ApiKeyLength={ApiKeyLength}",
        _httpClient.BaseAddress,
        _httpClient.DefaultRequestHeaders.Authorization != null,
        _httpClient.DefaultRequestHeaders.Authorization?.Scheme ?? "None",
        _httpClient.DefaultRequestHeaders.Contains("X-Api-Key"),
        _opts.ApiKey?.Length ?? 0);

    var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

    var body = await response.Content.ReadAsStringAsync(cancellationToken);

    _logger.LogInformation(
        "AgentHost Response: Status={StatusCode}, Body={Body}",
        (int)response.StatusCode,
        body);

    response.EnsureSuccessStatusCode();

    return JsonSerializer.Deserialize<BlueprintGenerationResponse>(body, JsonOptions)!;
}

    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        if (_httpClient.BaseAddress is null)
        {
            _logger.LogWarning("AgentHost.HealthCheck skipped — BaseAddress is null.");
            return false;
        }

        var healthPath = string.IsNullOrWhiteSpace(_opts.HealthCheckPath)
            ? "health"
            : _opts.HealthCheckPath.TrimStart('/');

        var uri = new Uri(_httpClient.BaseAddress, healthPath);

        try
        {
            using var response = await _httpClient.GetAsync(uri, cancellationToken);
            var healthy = response.IsSuccessStatusCode;
            _logger.LogInformation(
                "AgentHost.HealthCheck Uri={Uri} StatusCode={StatusCode} Healthy={Healthy}",
                uri, (int)response.StatusCode, healthy);
            return healthy;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "AgentHost.HealthCheck failed — could not reach {Uri}.", uri);
            return false;
        }
    }

    public async Task<byte[]?> DownloadPdfAsync(string pdfUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pdfUrl))
            return null;

        try
        {
            using var response = await _httpClient.GetAsync(pdfUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AgentHost.DownloadPdf failed — {StatusCode} from {Url}.", (int)response.StatusCode, pdfUrl);
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "AgentHost.DownloadPdf failed — could not reach {Url}.", pdfUrl);
            return null;
        }
    }

    public async Task<CreditCheckResult> CheckCreditsAsync(
        Guid tenantId, string? tenantName, CancellationToken cancellationToken = default)
    {
        if (_creditsOpts.BypassEnabled)
        {
            _logger.LogInformation(
                "AgentHost.CreditsCheck bypassed — tenant {TenantId} granted {Credits} fixed credits.",
                tenantId, _creditsOpts.BypassCreditsRemaining);
            return new CreditCheckResult(
                Allowed: true,
                Plan: "bypass",
                CreditsRemaining: _creditsOpts.BypassCreditsRemaining,
                IsUnlimited: false,
                NextResetDate: null,
                DenialReason: null);
        }

        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/credits/check");
            httpRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
            if (!string.IsNullOrWhiteSpace(tenantName))
                httpRequest.Headers.Add("X-Tenant-Name", tenantName);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == HttpStatusCode.PaymentRequired)
            {
                var denied = JsonSerializer.Deserialize<CreditCheckResponseDto>(body, JsonOptions);
                _logger.LogInformation(
                    "AgentHost.CreditsCheck denied — tenant {TenantId} has no credits remaining.", tenantId);
                return new CreditCheckResult(
                    Allowed: false,
                    Plan: null,
                    CreditsRemaining: 0,
                    IsUnlimited: false,
                    NextResetDate: denied?.NextResetDate,
                    DenialReason: denied?.Detail ?? "Insufficient credits.");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AgentHost.CreditsCheck failed — {StatusCode} for tenant {TenantId}. Failing open.",
                    (int)response.StatusCode, tenantId);
                return new CreditCheckResult(true, null, null, false, null, null);
            }

            var dto = JsonSerializer.Deserialize<CreditCheckResponseDto>(body, JsonOptions);
            return new CreditCheckResult(
                Allowed: true,
                Plan: dto?.Plan,
                CreditsRemaining: dto?.CreditsRemaining,
                IsUnlimited: dto?.IsUnlimited ?? false,
                NextResetDate: dto?.NextResetDate,
                DenialReason: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "AgentHost.CreditsCheck could not reach AgentHost for tenant {TenantId}. Failing open.", tenantId);
            return new CreditCheckResult(true, null, null, false, null, null);
        }
    }

    public async Task<CreditConsumeResult?> ConsumeCreditAsync(
        Guid tenantId, string feature, string? requestId, long executionTimeMs,
        CancellationToken cancellationToken = default)
    {
        if (_creditsOpts.BypassEnabled)
        {
            return new CreditConsumeResult(
                CreditsConsumed: 0,
                CreditsRemaining: _creditsOpts.BypassCreditsRemaining,
                IsUnlimited: false,
                Plan: "bypass",
                ResetDate: null);
        }

        try
        {
            var payload = JsonSerializer.Serialize(
                new { feature, requestId, executionTimeMs }, JsonOptions);

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/credits/consume");
            httpRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
            httpRequest.Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AgentHost.CreditsConsume failed — {StatusCode} for tenant {TenantId}, feature {Feature}.",
                    (int)response.StatusCode, tenantId, feature);
                return null;
            }

            var dto = JsonSerializer.Deserialize<CreditConsumeResponseDto>(body, JsonOptions);
            if (dto is null) return null;

            return new CreditConsumeResult(
                dto.CreditsConsumed, dto.CreditsRemaining, dto.IsUnlimited, dto.Plan, dto.ResetDate);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "AgentHost.CreditsConsume could not reach AgentHost for tenant {TenantId}, feature {Feature}.",
                tenantId, feature);
            return null;
        }
    }

    public async Task<CreditGrantResult?> GrantCreditsAsync(
        Guid tenantId, int credits, string reason, string? referenceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_opts.AdminApiKey))
        {
            _logger.LogWarning(
                "AgentHost.GrantCredits skipped — AgentHost:AdminApiKey is not configured.");
            return null;
        }

        try
        {
            var payload = JsonSerializer.Serialize(new { credits, reason, referenceId }, JsonOptions);

            var httpRequest = new HttpRequestMessage(
                HttpMethod.Post, $"api/admin/subscriptions/{tenantId}/grant-credits");
            httpRequest.Headers.Add("X-Admin-Key", _opts.AdminApiKey);
            httpRequest.Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AgentHost.GrantCredits failed — {StatusCode} for tenant {TenantId}, {Credits} credits.",
                    (int)response.StatusCode, tenantId, credits);
                return null;
            }

            var dto = JsonSerializer.Deserialize<CreditGrantResponseDto>(body, JsonOptions);
            if (dto is null) return null;

            return new CreditGrantResult(
                dto.CreditsGranted, dto.CreditsRemaining, dto.IsUnlimited, dto.Plan, dto.ResetDate);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "AgentHost.GrantCredits could not reach AgentHost for tenant {TenantId}, {Credits} credits.",
                tenantId, credits);
            return null;
        }
    }

    public async Task<CreditUsageSummary> GetUsageSummaryAsync(
        Guid tenantId, string feature, CancellationToken cancellationToken = default)
    {
        var empty = new CreditUsageSummary(feature, 0, 0);

        try
        {
            var httpRequest = new HttpRequestMessage(
                HttpMethod.Get, $"api/credits/usage-summary?feature={Uri.EscapeDataString(feature)}");
            httpRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AgentHost.UsageSummary failed — {StatusCode} for tenant {TenantId}, feature {Feature}.",
                    (int)response.StatusCode, tenantId, feature);
                return empty;
            }

            var dto = JsonSerializer.Deserialize<CreditUsageSummaryResponseDto>(body, JsonOptions);
            return dto is null
                ? empty
                : new CreditUsageSummary(dto.Feature, dto.RequestCount, dto.TotalCreditsConsumed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "AgentHost.UsageSummary could not reach AgentHost for tenant {TenantId}, feature {Feature}.",
                tenantId, feature);
            return empty;
        }
    }

    private sealed record CreditGrantResponseDto(
        int CreditsGranted, int? CreditsRemaining, bool IsUnlimited, string? Plan, DateTimeOffset? ResetDate);

    private sealed record CreditUsageSummaryResponseDto(
        string Feature, int RequestCount, int TotalCreditsConsumed);

    private sealed record CreditCheckResponseDto(
        string? Plan, int? CreditsRemaining, bool IsUnlimited, DateTimeOffset? NextResetDate, string? Detail);

    private sealed record CreditConsumeResponseDto(
        int CreditsConsumed, int? CreditsRemaining, bool IsUnlimited, string? Plan, DateTimeOffset? ResetDate);

    private static string MapStatusCode(HttpStatusCode statusCode, string body) =>
        (int)statusCode switch
        {
            400 => "AgentHost rejected the request (bad request). Check payload.",
            401 => "AgentHost authentication failed. Verify AGENT_HOST_API_KEY.",
            403 => "AgentHost API key rejected or insufficient permissions.",
            404 => "AgentHost endpoint not found. Check AGENT_HOST_BASE_URL.",
            408 => "AgentHost request timed out.",
            429 => "AgentHost rate limit exceeded. Retry after backoff.",
            500 => "AgentHost internal error.",
            503 => "AgentHost unavailable.",
            504 => "AgentHost request timed out (gateway timeout).",
            _   => $"AgentHost returned {(int)statusCode}: {Truncate(body, 300)}"
        };

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : string.Concat(s.AsSpan(0, max), "…");
}
