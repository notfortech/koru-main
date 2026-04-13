using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.DTOs.Insight;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;

namespace StudioTechBI.Infrastructure.Clients;

public class InsightEngineClient : IInsightEngineClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly IOptions<InsightEngineOptions> _options;
    private readonly ILogger<InsightEngineClient> _logger;

    public InsightEngineClient(
        HttpClient httpClient,
        IOptions<InsightEngineOptions> options,
        ILogger<InsightEngineClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ModelDto>> GenerateModelsAsync(GenerateModelRequest request, CancellationToken cancellationToken = default)
    {
        ApplyAuthHeader();

        using var response = await _httpClient.PostAsJsonAsync("api/models/generate", request, JsonOptions, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "InsightEngine generate failed: {StatusCode} {Body}",
                (int)response.StatusCode,
                Truncate(body, 2000));
            throw new HttpRequestException(
                $"InsightEngine generate failed with status {(int)response.StatusCode}: {Truncate(body, 500)}");
        }

        _logger.LogDebug("InsightEngine generate response: {Body}", Truncate(body, 4000));

        return ParseModelList(body);
    }

    public async Task<OrchestratorResultDto> SelectModelAsync(Guid modelId, string? validatedDataBlobPath, CancellationToken cancellationToken = default)
    {
        ApplyAuthHeader();

        var payload = new { validatedDataBlobPath };

        using var response = await _httpClient.PostAsJsonAsync(
            $"api/models/{modelId:D}/select",
            payload,
            JsonOptions,
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "InsightEngine select failed for model {ModelId}: {StatusCode} {Body}",
                modelId,
                (int)response.StatusCode,
                Truncate(body, 2000));
            return new OrchestratorResultDto
            {
                Success = false,
                Message = $"InsightEngine returned {(int)response.StatusCode}: {Truncate(body, 500)}"
            };
        }

        _logger.LogDebug("InsightEngine select response for model {ModelId}: {Body}", modelId, Truncate(body, 4000));

        return ParseOrchestratorResult(body);
    }

    private void ApplyAuthHeader()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
        var key = _options.Value.ApiKey?.Trim();
        if (!string.IsNullOrEmpty(key))
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
    }

    private static IReadOnlyList<ModelDto> ParseModelList(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<ModelDto>();

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
            return JsonSerializer.Deserialize<List<ModelDto>>(json, JsonOptions) ?? new List<ModelDto>();

        foreach (var prop in new[] { "models", "data", "items", "results" })
        {
            if (doc.RootElement.TryGetProperty(prop, out var arr) && arr.ValueKind == JsonValueKind.Array)
                return JsonSerializer.Deserialize<List<ModelDto>>(arr.GetRawText(), JsonOptions) ?? new List<ModelDto>();
        }

        var single = JsonSerializer.Deserialize<ModelDto>(json, JsonOptions);
        return single != null && single.Id != Guid.Empty ? new List<ModelDto> { single } : new List<ModelDto>();
    }

    private static OrchestratorResultDto ParseOrchestratorResult(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new OrchestratorResultDto { Success = false, Message = "Empty response from InsightEngine." };

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var dto = JsonSerializer.Deserialize<OrchestratorResultDto>(json, JsonOptions) ?? new OrchestratorResultDto();

            if (string.IsNullOrEmpty(dto.PowerBIDatasetId) && root.TryGetProperty("datasetId", out var ds))
                dto.PowerBIDatasetId = ds.GetString();

            if (string.IsNullOrEmpty(dto.ReportId) && root.TryGetProperty("reportId", out var rp))
                dto.ReportId = rp.GetString();

            if (!dto.Success && root.TryGetProperty("success", out var ok) && ok.ValueKind == JsonValueKind.True)
                dto.Success = true;

            return dto;
        }
        catch (JsonException)
        {
            return new OrchestratorResultDto { Success = false, Message = "Invalid JSON from InsightEngine select endpoint." };
        }
    }

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max)
            return s ?? "";
        return s[..max] + "…";
    }
}
