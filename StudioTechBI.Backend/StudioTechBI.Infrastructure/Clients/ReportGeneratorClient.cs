using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.ReportGenerator;
using StudioTechBI.Application.DTOs.VisualPlan;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.Infrastructure.Clients;

/// <summary>
/// Typed HTTP client for DashboardAgents.ReportAgent.Api.
///
/// This is the one integration in the platform that deliberately sends real
/// data (the connected Excel/CSV file) to a downstream service — but that
/// service is a sandboxed, deterministic Python/.NET engine with no AI/LLM
/// call anywhere in it, not a model. No AI ever sees this file's contents.
/// </summary>
public class ReportGeneratorClient : IReportGeneratorClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<ReportGeneratorClient> _logger;

    public ReportGeneratorClient(HttpClient httpClient, ILogger<ReportGeneratorClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<ReportTemplateDto>> ListTemplatesAsync(
        string correlationId, CancellationToken cancellationToken = default)
    {
        RequireBaseAddress();

        var request = new HttpRequestMessage(HttpMethod.Get, "api/templates");
        request.Headers.Add("X-Correlation-Id", correlationId);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "ReportGenerator.ListTemplatesFailed StatusCode={StatusCode} CorrelationId={CorrelationId}",
                (int)response.StatusCode, correlationId);
            throw new HttpRequestException(
                MapStatusCode(response.StatusCode, body), inner: null, statusCode: response.StatusCode);
        }

        return JsonSerializer.Deserialize<List<ReportTemplateDto>>(body, JsonOptions) ?? [];
    }

    public async Task<GeneratedReportDto> GenerateReportAsync(
        Stream fileStream,
        string fileName,
        string? templateId,
        string? filtersJson,
        string? htmlTemplateId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        RequireBaseAddress();

        // ── AI boundary log ────────────────────────────────────────────────
        // This call intentionally sends the real file — logged explicitly so
        // it's auditable as the one deliberate exception to "no raw data
        // leaves koru-main toward an AI boundary": the receiving service has
        // no AI/LLM in it at all (see ReportGeneratorClient's class remarks).
        _logger.LogInformation(
            "ReportGenerator.FileSentToEngine CorrelationId={CorrelationId} FileName={FileName} TemplateId={TemplateId}",
            correlationId, fileName, templateId ?? "(auto-match)");

        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(streamContent, "file", fileName);
        if (!string.IsNullOrWhiteSpace(templateId))
            content.Add(new StringContent(templateId), "templateId");
        if (!string.IsNullOrWhiteSpace(filtersJson))
            content.Add(new StringContent(filtersJson), "filters");
        if (!string.IsNullOrWhiteSpace(htmlTemplateId))
            content.Add(new StringContent(htmlTemplateId), "htmlTemplateId");

        var request = new HttpRequestMessage(HttpMethod.Post, "api/reports/generate") { Content = content };
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
                "ReportGenerator.GenerateTimeout DurationMs={DurationMs} CorrelationId={CorrelationId}",
                sw.ElapsedMilliseconds, correlationId);
            throw new HttpRequestException(
                "Report generation request timed out.", ex, HttpStatusCode.RequestTimeout);
        }

        using (response)
        {
            sw.Stop();
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "ReportGenerator.GenerateFailed StatusCode={StatusCode} DurationMs={DurationMs} CorrelationId={CorrelationId}",
                    (int)response.StatusCode, sw.ElapsedMilliseconds, correlationId);
                throw new HttpRequestException(
                    MapStatusCode(response.StatusCode, body), inner: null, statusCode: response.StatusCode);
            }

            GeneratedReportDto result;
            try
            {
                result = JsonSerializer.Deserialize<GeneratedReportDto>(body, JsonOptions)
                    ?? throw new InvalidOperationException("Report generation returned an empty response body.");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex,
                    "ReportGenerator.GenerateParseError CorrelationId={CorrelationId} Body={Body}",
                    correlationId, Truncate(body, 1000));
                throw new InvalidOperationException("Report generation response could not be parsed.", ex);
            }

            _logger.LogInformation(
                "ReportGenerator.GenerateSuccess TemplateId={TemplateId} DurationMs={DurationMs} CorrelationId={CorrelationId}",
                result.TemplateId, sw.ElapsedMilliseconds, correlationId);

            return result;
        }
    }

    public async Task<GeneratedReportDto> GenerateReportFromUrlAsync(
        string fileUrl,
        string fileName,
        string? templateId,
        string? filtersJson,
        string? htmlTemplateId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        RequireBaseAddress();

        // No raw bytes cross this call at all -- only a short-lived SAS URL koru-main already
        // minted. ReportAgent.Api's own C# layer fetches it; the Python subprocess still never
        // sees a URL or gets network access (see ReportGeneratorClient's class remarks + that
        // service's PythonAgentRunner doc comment).
        _logger.LogInformation(
            "ReportGenerator.FileUrlSentToEngine CorrelationId={CorrelationId} FileName={FileName} TemplateId={TemplateId}",
            correlationId, fileName, templateId ?? "(auto-match)");

        var request = new HttpRequestMessage(HttpMethod.Post, "api/reports/generate-from-url")
        {
            Content = JsonContent.Create(
                new
                {
                    fileUrl,
                    fileName,
                    templateId,
                    filters = filtersJson,
                    htmlTemplateId
                },
                options: JsonOptions)
        };
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
                "ReportGenerator.GenerateFromUrlTimeout DurationMs={DurationMs} CorrelationId={CorrelationId}",
                sw.ElapsedMilliseconds, correlationId);
            throw new HttpRequestException(
                "Report generation request timed out.", ex, HttpStatusCode.RequestTimeout);
        }

        using (response)
        {
            sw.Stop();
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "ReportGenerator.GenerateFromUrlFailed StatusCode={StatusCode} DurationMs={DurationMs} CorrelationId={CorrelationId}",
                    (int)response.StatusCode, sw.ElapsedMilliseconds, correlationId);
                throw new HttpRequestException(
                    MapStatusCode(response.StatusCode, body), inner: null, statusCode: response.StatusCode);
            }

            GeneratedReportDto result;
            try
            {
                result = JsonSerializer.Deserialize<GeneratedReportDto>(body, JsonOptions)
                    ?? throw new InvalidOperationException("Report generation returned an empty response body.");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex,
                    "ReportGenerator.GenerateFromUrlParseError CorrelationId={CorrelationId} Body={Body}",
                    correlationId, Truncate(body, 1000));
                throw new InvalidOperationException("Report generation response could not be parsed.", ex);
            }

            _logger.LogInformation(
                "ReportGenerator.GenerateFromUrlSuccess TemplateId={TemplateId} DurationMs={DurationMs} CorrelationId={CorrelationId}",
                result.TemplateId, sw.ElapsedMilliseconds, correlationId);

            return result;
        }
    }

    public async Task<List<HtmlTemplateCandidateDto>> MatchHtmlTemplateAsync(
        Stream fileStream,
        string fileName,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        RequireBaseAddress();

        _logger.LogInformation(
            "ReportGenerator.HtmlMatchFileSentToEngine CorrelationId={CorrelationId} FileName={FileName}",
            correlationId, fileName);

        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(streamContent, "file", fileName);

        var request = new HttpRequestMessage(HttpMethod.Post, "api/reports/match-html-template") { Content = content };
        request.Headers.Add("X-Correlation-Id", correlationId);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "ReportGenerator.HtmlMatchFailed StatusCode={StatusCode} CorrelationId={CorrelationId}",
                (int)response.StatusCode, correlationId);
            throw new HttpRequestException(
                MapStatusCode(response.StatusCode, body), inner: null, statusCode: response.StatusCode);
        }

        HtmlTemplateMatchWireResult? wire;
        try
        {
            wire = JsonSerializer.Deserialize<HtmlTemplateMatchWireResult>(body, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "ReportGenerator.HtmlMatchParseError CorrelationId={CorrelationId} Body={Body}",
                correlationId, Truncate(body, 1000));
            throw new InvalidOperationException("HTML template match response could not be parsed.", ex);
        }

        return wire?.Candidates ?? [];
    }

    public async Task<ColumnProfileResultDto> ProfileColumnsAsync(
        Stream fileStream,
        string fileName,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        RequireBaseAddress();

        _logger.LogInformation(
            "ReportGenerator.ProfileColumnsFileSentToEngine CorrelationId={CorrelationId} FileName={FileName}",
            correlationId, fileName);

        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(streamContent, "file", fileName);

        var request = new HttpRequestMessage(HttpMethod.Post, "api/reports/profile-columns") { Content = content };
        request.Headers.Add("X-Correlation-Id", correlationId);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "ReportGenerator.ProfileColumnsFailed StatusCode={StatusCode} CorrelationId={CorrelationId}",
                (int)response.StatusCode, correlationId);
            throw new HttpRequestException(
                MapStatusCode(response.StatusCode, body), inner: null, statusCode: response.StatusCode);
        }

        try
        {
            return JsonSerializer.Deserialize<ColumnProfileResultDto>(body, JsonOptions)
                ?? throw new InvalidOperationException("Column profiling returned an empty response body.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "ReportGenerator.ProfileColumnsParseError CorrelationId={CorrelationId} Body={Body}",
                correlationId, Truncate(body, 1000));
            throw new InvalidOperationException("Column profiling response could not be parsed.", ex);
        }
    }

    public async Task<ChartFromSpecResultDto> GenerateChartsFromSpecAsync(
        Stream fileStream,
        string fileName,
        List<VisualPlanChartSpecDto> chartSpecs,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        RequireBaseAddress();

        // ── AI boundary log ────────────────────────────────────────────────
        // Same intentional exception as GenerateReportAsync -- the real file goes to this
        // no-AI, deterministic engine. Logged explicitly for the same auditability reason.
        _logger.LogInformation(
            "ReportGenerator.ChartFromSpecFileSentToEngine CorrelationId={CorrelationId} FileName={FileName} ChartSpecCount={ChartSpecCount}",
            correlationId, fileName, chartSpecs.Count);

        var wireSpecs = chartSpecs.Select(s => new ChartFromSpecRequestItemDto(
            Id: s.Id,
            Measure: s.Measure,
            Dimension: s.Dimension,
            ChartType: s.ChartType,
            ValueKind: s.ValueKind,
            DrillPath: s.DrillPath,
            FilterField: s.FilterField,
            PairId: s.PairId)).ToList();
        var chartSpecsJson = JsonSerializer.Serialize(wireSpecs, JsonOptions);

        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(streamContent, "file", fileName);
        content.Add(new StringContent(chartSpecsJson), "chartSpecs");

        var request = new HttpRequestMessage(HttpMethod.Post, "api/reports/chart-from-spec") { Content = content };
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
                "ReportGenerator.ChartFromSpecTimeout DurationMs={DurationMs} CorrelationId={CorrelationId}",
                sw.ElapsedMilliseconds, correlationId);
            throw new HttpRequestException(
                "Chart-from-spec request timed out.", ex, HttpStatusCode.RequestTimeout);
        }

        using (response)
        {
            sw.Stop();
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "ReportGenerator.ChartFromSpecFailed StatusCode={StatusCode} DurationMs={DurationMs} CorrelationId={CorrelationId}",
                    (int)response.StatusCode, sw.ElapsedMilliseconds, correlationId);
                throw new HttpRequestException(
                    MapStatusCode(response.StatusCode, body), inner: null, statusCode: response.StatusCode);
            }

            ChartFromSpecResultDto result;
            try
            {
                result = JsonSerializer.Deserialize<ChartFromSpecResultDto>(body, JsonOptions)
                    ?? throw new InvalidOperationException("Chart-from-spec returned an empty response body.");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex,
                    "ReportGenerator.ChartFromSpecParseError CorrelationId={CorrelationId} Body={Body}",
                    correlationId, Truncate(body, 1000));
                throw new InvalidOperationException("Chart-from-spec response could not be parsed.", ex);
            }

            _logger.LogInformation(
                "ReportGenerator.ChartFromSpecSuccess ChartCount={ChartCount} DurationMs={DurationMs} CorrelationId={CorrelationId}",
                result.Charts.Count, sw.ElapsedMilliseconds, correlationId);

            return result;
        }
    }

    public async Task PushHtmlTemplateRegistryAsync(
        List<HtmlTemplateManifestPushDto> manifests,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        RequireBaseAddress();

        var request = new HttpRequestMessage(HttpMethod.Post, "api/html-templates/registry")
        {
            Content = JsonContent.Create(manifests, options: JsonOptions)
        };
        request.Headers.Add("X-Correlation-Id", correlationId);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "ReportGenerator.HtmlRegistryPushFailed StatusCode={StatusCode} CorrelationId={CorrelationId}",
                (int)response.StatusCode, correlationId);
            throw new HttpRequestException(
                MapStatusCode(response.StatusCode, body), inner: null, statusCode: response.StatusCode);
        }
    }

    private sealed record HtmlTemplateMatchWireResult(List<HtmlTemplateCandidateDto> Candidates);

    private void RequireBaseAddress()
    {
        if (_httpClient.BaseAddress is null)
            throw new InvalidOperationException(
                "ReportGeneratorClient: HttpClient.BaseAddress is null. Verify ReportGenerator:BaseUrl is configured.");
    }

    private static string MapStatusCode(HttpStatusCode statusCode, string body) =>
        (int)statusCode switch
        {
            400 => $"Report generator rejected the request: {Truncate(body, 300)}",
            401 => "Report generator authentication failed. Verify ReportGenerator:ApiKey.",
            403 => "Report generator API key rejected or insufficient permissions.",
            404 => "Report generator endpoint not found. Check ReportGenerator:BaseUrl.",
            429 => "Report generator rate limit exceeded.",
            502 => "Report generator could not process the supplied file.",
            504 => "Report generation timed out or was aborted.",
            _ => $"Report generator returned {(int)statusCode}: {Truncate(body, 300)}"
        };

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : string.Concat(s.AsSpan(0, max), "…");
}
