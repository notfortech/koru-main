using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.InsightsEngine;

namespace StudioTechBI.Infrastructure.Clients;

/// <summary>Typed client for the external InsightsEngine transformations suggestion endpoint.</summary>
public sealed class InsightsEngineClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<InsightsEngineClient> _logger;

    public InsightsEngineClient(HttpClient httpClient, ILogger<InsightsEngineClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<TransformSuggestResponse> SuggestTransformationsAsync(
        TransformSuggestRequest request,
        CancellationToken ct = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/ai/transformations/suggest",
            request,
            JsonOptions,
            ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "InsightsEngine suggest failed: {StatusCode} {Body}",
                (int)response.StatusCode,
                Truncate(body, 2000));
            response.EnsureSuccessStatusCode();
        }

        if (string.IsNullOrWhiteSpace(body))
            return new TransformSuggestResponse();

        return JsonSerializer.Deserialize<TransformSuggestResponse>(body, JsonOptions) ?? new TransformSuggestResponse();
    }

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max)
            return s ?? "";
        return s[..max] + "…";
    }
}

