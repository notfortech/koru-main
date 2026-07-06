using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.DTOs.ReportDesigner;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;

namespace StudioTechBI.Infrastructure.Clients;

/// <summary>
/// Typed HTTP client for the Report Designer AI endpoint.
/// Sends only structural schema metadata (table/column names and types).
/// Never sends actual data values, connection strings, passwords, or tokens.
/// All AI boundary events are logged via structured log templates.
/// </summary>
public class ReportDesignerClient : IReportDesignerClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<ReportDesignerClient> _logger;
    private readonly ReportDesignerOptions _opts;

    public ReportDesignerClient(
        HttpClient httpClient,
        ILogger<ReportDesignerClient> logger,
        IOptions<ReportDesignerOptions> options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _opts = options.Value;
    }

    public async Task<GenerateReportModelResponse> GenerateReportModelAsync(
        GenerateReportModelRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (_httpClient.BaseAddress is null)
            throw new InvalidOperationException(
                "ReportDesignerClient: HttpClient.BaseAddress is null. Verify ReportDesigner:BaseUrl is configured.");

        if (string.IsNullOrWhiteSpace(correlationId))
            throw new InvalidOperationException("correlationId must not be empty.");

        string payloadJson;
        try
        {
            payloadJson = JsonSerializer.Serialize(request, JsonOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to serialize GenerateReportModelRequest.", ex);
        }

        // ── AI boundary log — BEFORE sending ─────────────────────────────────
        // Logs only structural metadata: table names, column counts, source.
        // Never logs actual data values, connection strings, passwords, or API keys.
        _logger.LogInformation(
            "ReportDesigner.SchemaSentToAI CorrelationId={CorrelationId} Source={Source} " +
            "FileName={FileName} TableCount={TableCount} Tables={TableNames} " +
            "TotalColumns={TotalColumns} PreferredTheme={PreferredTheme} PayloadBytes={PayloadBytes}",
            correlationId,
            request.Schema.Source,
            request.Schema.FileName,
            request.Schema.Tables.Count,
            string.Join(",", request.Schema.Tables.Select(t => t.TableName)),
            request.Schema.Tables.Sum(t => t.Columns.Count),
            request.PreferredTheme ?? "(none)",
            payloadJson.Length);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/report-designer/generate");
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
                "ReportDesigner.AIRequestTimeout DurationMs={DurationMs} CorrelationId={CorrelationId}",
                sw.ElapsedMilliseconds, correlationId);
            throw new HttpRequestException(
                "Report Designer AI request timed out.", ex, HttpStatusCode.RequestTimeout);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            _logger.LogError(
                "ReportDesigner.AIRequestFailed ExceptionType={ExceptionType} Message={Message} " +
                "DurationMs={DurationMs} CorrelationId={CorrelationId}",
                ex.GetType().Name, ex.Message, sw.ElapsedMilliseconds, correlationId);
            throw;
        }

        using (response)
        {
            sw.Stop();
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "ReportDesigner.AIRequestFailed StatusCode={StatusCode} DurationMs={DurationMs} " +
                    "CorrelationId={CorrelationId}",
                    (int)response.StatusCode, sw.ElapsedMilliseconds, correlationId);

                throw new HttpRequestException(
                    MapStatusCode(response.StatusCode, body),
                    inner: null,
                    statusCode: response.StatusCode);
            }

            GenerateReportModelResponse result;
            try
            {
                result = JsonSerializer.Deserialize<GenerateReportModelResponse>(body, JsonOptions)
                    ?? throw new InvalidOperationException("Report Designer AI returned an empty response body.");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex,
                    "ReportDesigner.AIResponseParseError CorrelationId={CorrelationId} Body={Body}",
                    correlationId, Truncate(body, 1000));
                throw new InvalidOperationException(
                    "Report Designer AI response could not be parsed.", ex);
            }

            // ── AI boundary log — AFTER receiving ────────────────────────────
            // Logs only structural summary of what was received — no data values.
            _logger.LogInformation(
                "ReportDesigner.AIResponseReceived CorrelationId={CorrelationId} " +
                "FactTable={FactTable} DimensionCount={DimensionCount} " +
                "RelationshipCount={RelationshipCount} TemplatesReturned={TemplatesReturned} " +
                "DurationMs={DurationMs}",
                correlationId,
                result.StarSchema.FactTable,
                result.StarSchema.DimensionTables.Count,
                result.StarSchema.Relationships.Count,
                result.Templates.Count,
                sw.ElapsedMilliseconds);

            return result with { CorrelationId = correlationId, DurationMs = sw.ElapsedMilliseconds };
        }
    }

    private static string MapStatusCode(HttpStatusCode statusCode, string body) =>
        (int)statusCode switch
        {
            400 => "Report Designer AI rejected the schema payload.",
            401 => "Report Designer AI authentication failed. Verify REPORT_DESIGNER_API_KEY.",
            403 => "Report Designer AI API key rejected or insufficient permissions.",
            404 => "Report Designer AI endpoint not found. Check REPORT_DESIGNER_BASE_URL.",
            429 => "Report Designer AI rate limit exceeded.",
            500 => "Report Designer AI internal error.",
            503 => "Report Designer AI unavailable.",
            _   => $"Report Designer AI returned {(int)statusCode}: {Truncate(body, 300)}"
        };

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : string.Concat(s.AsSpan(0, max), "…");
}
