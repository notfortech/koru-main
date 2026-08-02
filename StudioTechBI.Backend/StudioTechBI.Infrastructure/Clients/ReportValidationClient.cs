using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.DTOs.ReportValidation;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;

namespace StudioTechBI.Infrastructure.Clients;

/// <summary>
/// Typed HTTP client for DashboardAgents.ReportValidationApi — same shape/conventions as
/// ReportGeneratorClient (the file is re-sent here for the same reason as the report generator
/// call: this downstream service is a sandboxed automation runner with no AI/LLM in it).
/// </summary>
public class ReportValidationClient : IReportValidationClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<ReportValidationOptions> _options;
    private readonly ILogger<ReportValidationClient> _logger;

    public ReportValidationClient(HttpClient httpClient, IOptionsMonitor<ReportValidationOptions> options, ILogger<ReportValidationClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<RenderingHealthResponse> RunRenderingHealthAsync(
        Stream fileStream,
        string fileName,
        string? templateId,
        string? filtersJson,
        string authToken,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        RequireBaseAddress();
        var appBaseUrl = _options.CurrentValue.AppBaseUrl;
        if (string.IsNullOrWhiteSpace(appBaseUrl))
            throw new InvalidOperationException("ReportValidation:AppBaseUrl is not configured.");

        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(streamContent, "file", fileName);
        if (!string.IsNullOrWhiteSpace(templateId))
            content.Add(new StringContent(templateId), "templateId");
        if (!string.IsNullOrWhiteSpace(filtersJson))
            content.Add(new StringContent(filtersJson), "filters");
        content.Add(new StringContent(appBaseUrl), "appBaseUrl");
        content.Add(new StringContent(authToken), "authToken");

        var request = new HttpRequestMessage(HttpMethod.Post, "api/validations/rendering-health") { Content = content };
        request.Headers.Add("X-Correlation-Id", correlationId);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            _logger.LogError(
                "ReportValidation.RenderingHealthTimeout DurationMs={DurationMs} CorrelationId={CorrelationId}",
                sw.ElapsedMilliseconds, correlationId);
            throw new HttpRequestException(
                "Rendering health check timed out.", ex, HttpStatusCode.RequestTimeout);
        }

        using (response)
        {
            sw.Stop();
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "ReportValidation.RenderingHealthFailed StatusCode={StatusCode} DurationMs={DurationMs} CorrelationId={CorrelationId}",
                    (int)response.StatusCode, sw.ElapsedMilliseconds, correlationId);
                throw new HttpRequestException(
                    MapStatusCode(response.StatusCode, body), inner: null, statusCode: response.StatusCode);
            }

            RenderingHealthResponse result;
            try
            {
                result = JsonSerializer.Deserialize<RenderingHealthResponse>(body, JsonOptions)
                    ?? throw new InvalidOperationException("Rendering health check returned an empty response body.");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex,
                    "ReportValidation.RenderingHealthParseError CorrelationId={CorrelationId} Body={Body}",
                    correlationId, Truncate(body, 1000));
                throw new InvalidOperationException("Rendering health check response could not be parsed.", ex);
            }

            _logger.LogInformation(
                "ReportValidation.RenderingHealthSuccess CheckCount={CheckCount} DurationMs={DurationMs} CorrelationId={CorrelationId}",
                result.Checks.Count, sw.ElapsedMilliseconds, correlationId);

            return result;
        }
    }

    private void RequireBaseAddress()
    {
        if (_httpClient.BaseAddress is null)
            throw new InvalidOperationException(
                "ReportValidationClient: HttpClient.BaseAddress is null. Verify ReportValidation:BaseUrl is configured.");
    }

    private static string MapStatusCode(HttpStatusCode statusCode, string body) =>
        (int)statusCode switch
        {
            400 => $"Report validation service rejected the request: {Truncate(body, 300)}",
            401 => "Report validation service authentication failed. Verify ReportValidation:ApiKey.",
            403 => "Report validation service API key rejected or insufficient permissions.",
            404 => "Report validation service endpoint not found. Check ReportValidation:BaseUrl.",
            429 => "Report validation service rate limit exceeded.",
            502 => "Report validation service could not process the supplied file.",
            504 => "Rendering health check timed out or was aborted.",
            _ => $"Report validation service returned {(int)statusCode}: {Truncate(body, 300)}"
        };

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : string.Concat(s.AsSpan(0, max), "…");
}
