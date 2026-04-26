using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.InsightsEngine;
using StudioTechBI.Application.DTOs.InsightsEngine.Canonical;
using StudioTechBI.Application.DTOs.Templates;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.Infrastructure.Clients;

/// <summary>Typed client for the external InsightsEngine transformations suggestion endpoint.</summary>
public sealed class InsightsEngineClient : IInsightsEngineTemplateMappingClient
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

    public async Task<ModelSuggestResponse> SuggestModelsAsync(
        ModelSuggestRequest request,
        CancellationToken ct = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/ai/models/suggest",
            request,
            JsonOptions,
            ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "InsightsEngine model suggest failed: {StatusCode} {Body}",
                (int)response.StatusCode,
                Truncate(body, 2000));
            response.EnsureSuccessStatusCode();
        }

        if (string.IsNullOrWhiteSpace(body))
            return new ModelSuggestResponse();

        return JsonSerializer.Deserialize<ModelSuggestResponse>(body, JsonOptions) ?? new ModelSuggestResponse();
    }

    public async Task<PlansGenerateResponse> GeneratePlansAsync(
        PlansGenerateRequest request,
        CancellationToken ct = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/ai/plans/generate",
            request,
            JsonOptions,
            ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "InsightsEngine plans generate failed: {StatusCode} {Body}",
                (int)response.StatusCode,
                Truncate(body, 2000));
            response.EnsureSuccessStatusCode();
        }

        if (string.IsNullOrWhiteSpace(body))
            return new PlansGenerateResponse();

        return JsonSerializer.Deserialize<PlansGenerateResponse>(body, JsonOptions) ?? new PlansGenerateResponse();
    }

    public async Task<TemplateMappingPreview> RefineTemplateMappingAsync(
        IReadOnlyList<string> clientColumns,
        IReadOnlyList<string> requiredTemplateColumns,
        IReadOnlyList<string> optionalTemplateColumns,
        CancellationToken ct = default)
    {
        var req = new
        {
            clientColumns,
            requiredTemplateColumns,
            optionalTemplateColumns
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "api/ai/templates/map",
            req,
            JsonOptions,
            ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "InsightsEngine template map failed: {StatusCode} {Body}",
                (int)response.StatusCode,
                Truncate(body, 2000));
            response.EnsureSuccessStatusCode();
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return new TemplateMappingPreview { Notes = "empty response from mapping service" };
        }

        // Deserialize loosely (matching the InsightsEngine.Api DTO contract).
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var mapping = new List<TemplateColumnMapping>();
        if (root.TryGetProperty("mapping", out var mp) && mp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in mp.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var t = item.TryGetProperty("templateColumn", out var tc) && tc.ValueKind == JsonValueKind.String ? tc.GetString() : null;
                var c = item.TryGetProperty("clientColumn", out var cc) && cc.ValueKind == JsonValueKind.String ? cc.GetString() : null;
                if (string.IsNullOrWhiteSpace(t) || string.IsNullOrWhiteSpace(c)) continue;
                var tf = item.TryGetProperty("transform", out var tr) && tr.ValueKind == JsonValueKind.String ? tr.GetString() : null;
                mapping.Add(new TemplateColumnMapping { TemplateColumn = t!, ClientColumn = c!, Transform = tf });
            }
        }

        var transformations = new List<string>();
        if (root.TryGetProperty("transformations", out var trArr) && trArr.ValueKind == JsonValueKind.Array)
        {
            transformations = trArr.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString() ?? "")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }

        var preview = new TemplateMappingPreview
        {
            Mapping = mapping,
            Transformations = transformations
        };

        if (root.TryGetProperty("confidence", out var conf) && conf.ValueKind == JsonValueKind.Number && conf.TryGetDouble(out var cval))
        {
            preview.Notes = $"ai_confidence={Math.Round(cval, 4)}";
        }

        return preview;
    }

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max)
            return s ?? "";
        return s[..max] + "…";
    }
}

