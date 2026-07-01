using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.Blueprints;
using StudioTechBI.Application.Interfaces;

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

    public AgentHostClient(HttpClient httpClient, ILogger<AgentHostClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<BlueprintGenerationResponse> GenerateBlueprintAsync(
        GenerateBlueprintRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Calling AgentHost /api/blueprints/generate. CorrelationId={CorrelationId} Tenant={TenantId} Client={ClientId}",
            correlationId, request.TenantId, request.ClientId);

        var requestId = Guid.NewGuid();
        var agentHostRequest = AgentHostBlueprintRequest.From(request, requestId);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/blueprints/generate");
        httpRequest.Headers.Add("X-Correlation-Id", correlationId);
        httpRequest.Content = JsonContent.Create(agentHostRequest, options: JsonOptions);

        // Diagnostic: confirm the factory-configured HttpClient (with BaseAddress) is in use.
        // BaseAddress null here means DI bypassed IHttpClientFactory — see AgentHostIntegrationExtensions.
        _logger.LogInformation(
            "AgentHost BaseAddress={BaseAddress} RequestUri={RequestUri}",
            _httpClient.BaseAddress,
            httpRequest.RequestUri);

        var sw = System.Diagnostics.Stopwatch.StartNew();

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        sw.Stop();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogInformation(
            "AgentHost responded: Status={StatusCode} Size={ResponseSize}B Duration={ElapsedMs}ms CorrelationId={CorrelationId}",
            (int)response.StatusCode, body.Length, sw.ElapsedMilliseconds, correlationId);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "AgentHost error: {StatusCode} CorrelationId={CorrelationId} Body={Body}",
                (int)response.StatusCode, correlationId, Truncate(body, 1000));

            throw new HttpRequestException(
                $"AgentHost returned {(int)response.StatusCode}: {Truncate(body, 500)}",
                inner: null,
                statusCode: response.StatusCode);
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
            throw new InvalidOperationException("AgentHost response could not be parsed as BlueprintGenerationResponse.", ex);
        }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : string.Concat(s.AsSpan(0, max), "…");
}
