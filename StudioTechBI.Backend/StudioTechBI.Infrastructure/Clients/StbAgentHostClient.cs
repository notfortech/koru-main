using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.Blueprint;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.Infrastructure.Clients;

/// <summary>Typed client for the STBI AgentHost blueprint generation API.</summary>
public sealed class StbAgentHostClient : IStbAgentHostClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<StbAgentHostClient> _logger;

    public StbAgentHostClient(HttpClient httpClient, ILogger<StbAgentHostClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<StbAgentHostResponseDto> GenerateBlueprintAsync(
        StbAgentHostRequestDto request,
        string tenantId,
        string tenantName,
        CancellationToken ct = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/blueprints/generate");
        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

        if (!string.IsNullOrWhiteSpace(tenantId))
            httpRequest.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);
        if (!string.IsNullOrWhiteSpace(tenantName))
            httpRequest.Headers.TryAddWithoutValidation("X-Tenant-Name", tenantName);

        using var response = await _httpClient.SendAsync(httpRequest, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "StbAgentHost blueprint generate failed: {StatusCode} {Body}",
                (int)response.StatusCode,
                Truncate(body, 2000));
            response.EnsureSuccessStatusCode();
        }

        if (string.IsNullOrWhiteSpace(body))
            return new StbAgentHostResponseDto { Status = "Failed" };

        return JsonSerializer.Deserialize<StbAgentHostResponseDto>(body, JsonOptions)
            ?? new StbAgentHostResponseDto { Status = "Failed" };
    }

    public async Task<Stream?> DownloadPdfAsync(string agentHostPdfUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(agentHostPdfUrl))
            return null;

        var response = await _httpClient.GetAsync(agentHostPdfUrl, HttpCompletionOption.ResponseHeadersRead, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("StbAgentHost PDF download failed: {StatusCode} for {Url}",
                (int)response.StatusCode, agentHostPdfUrl);
            response.EnsureSuccessStatusCode();
        }

        return await response.Content.ReadAsStreamAsync(ct);
    }

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? "";
        return s[..max] + "…";
    }
}
