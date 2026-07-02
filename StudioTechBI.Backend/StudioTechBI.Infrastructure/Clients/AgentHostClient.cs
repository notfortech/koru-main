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
        // ── Pre-flight validation ──────────────────────────────────────────
        if (_httpClient.BaseAddress is null)
            throw new InvalidOperationException(
                "AgentHostClient: HttpClient.BaseAddress is null. " +
                "Verify that IAgentHostClient (not AgentHostClient) is registered via AddHttpClient<IAgentHostClient, AgentHostClient>().");

        if (string.IsNullOrWhiteSpace(correlationId))
            throw new InvalidOperationException(
                "AgentHostClient.GenerateBlueprintAsync: correlationId must not be empty.");

        string payloadJson;
        try
        {
            var agentHostRequest = AgentHostBlueprintRequest.From(request, Guid.NewGuid());
            payloadJson = JsonSerializer.Serialize(agentHostRequest, JsonOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "AgentHostClient: Failed to serialize GenerateBlueprintRequest payload.", ex);
        }

        var absoluteUri = new Uri(_httpClient.BaseAddress, "api/blueprints/generate");

        // NOTE: ApiKey is NEVER logged.
        _logger.LogInformation(
            "AgentHost.RequestStarted CorrelationId={CorrelationId} BaseAddress={BaseAddress} " +
            "AbsoluteUri={AbsoluteUri} Method={Method} PayloadSizeBytes={PayloadSizeBytes} " +
            "TimeoutSeconds={TimeoutSeconds} TenantId={TenantId} ClientId={ClientId}",
            correlationId,
            _httpClient.BaseAddress,
            absoluteUri,
            "POST",
            payloadJson.Length,
            _opts.TimeoutSeconds,
            request.TenantId,
            request.ClientId);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/blueprints/generate");
        httpRequest.Headers.Add("X-Correlation-Id", correlationId);
        httpRequest.Content = new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json");

        var sw = System.Diagnostics.Stopwatch.StartNew();

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            _logger.LogError(
                "AgentHost.RequestFailed ExceptionType={ExceptionType} Message={Message} " +
                "InnerException={InnerException} DurationMs={DurationMs} CorrelationId={CorrelationId}",
                ex.GetType().Name, ex.Message, ex.InnerException?.Message,
                sw.ElapsedMilliseconds, correlationId);
            throw new HttpRequestException(
                "AgentHost request timed out (client-side timeout).",
                ex,
                HttpStatusCode.RequestTimeout);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            _logger.LogError(
                "AgentHost.RequestFailed ExceptionType={ExceptionType} Message={Message} " +
                "StackTrace={StackTrace} InnerException={InnerException} DurationMs={DurationMs} CorrelationId={CorrelationId}",
                ex.GetType().Name, ex.Message,
                Truncate(ex.StackTrace ?? string.Empty, 500),
                ex.InnerException?.Message,
                sw.ElapsedMilliseconds, correlationId);
            throw;
        }

        using (response)
        {
            sw.Stop();
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogInformation(
                "AgentHost.RequestCompleted StatusCode={StatusCode} DurationMs={DurationMs} " +
                "ResponseSizeBytes={ResponseSizeBytes} CorrelationId={CorrelationId}",
                (int)response.StatusCode, sw.ElapsedMilliseconds, body.Length, correlationId);

            if (!response.IsSuccessStatusCode)
            {
                var mappedMessage = MapStatusCode(response.StatusCode, body);
                _logger.LogWarning(
                    "AgentHost.RequestFailed StatusCode={StatusCode} MappedMessage={MappedMessage} " +
                    "DurationMs={DurationMs} CorrelationId={CorrelationId}",
                    (int)response.StatusCode, mappedMessage, sw.ElapsedMilliseconds, correlationId);

                throw new HttpRequestException(mappedMessage, inner: null, statusCode: response.StatusCode);
            }

            try
            {
                var result = JsonSerializer.Deserialize<BlueprintGenerationResponse>(body, JsonOptions)
                    ?? throw new InvalidOperationException("AgentHost returned an empty response body.");
                return result;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex,
                    "Failed to deserialise AgentHost response. CorrelationId={CorrelationId} Body={Body}",
                    correlationId, Truncate(body, 2000));
                throw new InvalidOperationException(
                    "AgentHost response could not be parsed as BlueprintGenerationResponse.", ex);
            }
        }
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
