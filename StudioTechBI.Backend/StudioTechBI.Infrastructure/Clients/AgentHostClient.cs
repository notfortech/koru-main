using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.DTOs.Blueprints;
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

    public AgentHostClient(
        HttpClient httpClient,
        ILogger<AgentHostClient> logger,
        IOptions<AgentHostOptions> options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _opts = options.Value;
    }

    public async Task<BlueprintGenerationResponse> GenerateBlueprintAsync(
    GenerateBlueprintRequest request,
    string correlationId,
    CancellationToken cancellationToken = default)
{
    var payload = JsonSerializer.Serialize(
        AgentHostBlueprintRequest.From(request, Guid.NewGuid()),
        JsonOptions);

    var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/blueprints/generate");
    httpRequest.Headers.Add("X-Correlation-Id", correlationId);
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
