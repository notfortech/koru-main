using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.DTOs.ReportDesigner;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;

namespace StudioTechBI.Infrastructure.Clients;

/// <summary>
/// Typed HTTP client for the stbi_transformers pipeline.
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

        // ── AI boundary log — BEFORE sending ─────────────────────────────────
        // Logs only structural metadata: table names, column counts, source.
        // Never logs actual data values, connection strings, passwords, or API keys.
        _logger.LogInformation(
            "ReportDesigner.SchemaSentToAI CorrelationId={CorrelationId} Source={Source} " +
            "FileName={FileName} TableCount={TableCount} Tables={TableNames} " +
            "TotalColumns={TotalColumns} PreferredTheme={PreferredTheme}",
            correlationId,
            request.Schema.Source,
            request.Schema.FileName,
            request.Schema.Tables.Count,
            string.Join(",", request.Schema.Tables.Select(t => t.TableName)),
            request.Schema.Tables.Sum(t => t.Columns.Count),
            request.PreferredTheme ?? "(none)");

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // ── Step 1: Connect — send schema, receive sessionId ─────────────────
        var connectPayload = JsonSerializer.Serialize(request, JsonOptions);
        var connectRequest = new HttpRequestMessage(HttpMethod.Post, "api/pipeline/connect");
        connectRequest.Headers.Add("X-Correlation-Id", correlationId);
        connectRequest.Content = new StringContent(connectPayload, System.Text.Encoding.UTF8, "application/json");

        HttpResponseMessage connectResponse;
        try
        {
            connectResponse = await _httpClient.SendAsync(connectRequest, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            _logger.LogError(
                "ReportDesigner.ConnectTimeout DurationMs={DurationMs} CorrelationId={CorrelationId}",
                sw.ElapsedMilliseconds, correlationId);
            throw new HttpRequestException(
                "Report Designer connect request timed out.", ex, HttpStatusCode.RequestTimeout);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            _logger.LogError(
                "ReportDesigner.ConnectFailed ExceptionType={ExceptionType} Message={Message} " +
                "DurationMs={DurationMs} CorrelationId={CorrelationId}",
                ex.GetType().Name, ex.Message, sw.ElapsedMilliseconds, correlationId);
            throw;
        }

        string sessionId;
        using (connectResponse)
        {
            var connectBody = await connectResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!connectResponse.IsSuccessStatusCode)
            {
                sw.Stop();
                _logger.LogWarning(
                    "ReportDesigner.ConnectFailed StatusCode={StatusCode} DurationMs={DurationMs} " +
                    "CorrelationId={CorrelationId}",
                    (int)connectResponse.StatusCode, sw.ElapsedMilliseconds, correlationId);

                throw new HttpRequestException(
                    MapStatusCode(connectResponse.StatusCode, connectBody),
                    inner: null,
                    statusCode: connectResponse.StatusCode);
            }

            PipelineConnectResponse connectResult;
            try
            {
                connectResult = JsonSerializer.Deserialize<PipelineConnectResponse>(connectBody, JsonOptions)
                    ?? throw new InvalidOperationException("Pipeline connect returned an empty response body.");
            }
            catch (JsonException ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "ReportDesigner.ConnectParseError CorrelationId={CorrelationId} Body={Body}",
                    correlationId, Truncate(connectBody, 1000));
                throw new InvalidOperationException(
                    "Report Designer connect response could not be parsed.", ex);
            }

            sessionId = connectResult.SessionId;
        }

        _logger.LogInformation(
            "ReportDesigner.ConnectSuccess SessionId={SessionId} CorrelationId={CorrelationId}",
            sessionId, correlationId);

        // ── Step 2: Generate — send preferredTheme, receive blueprint ────────
        var generatePayload = JsonSerializer.Serialize(
            new { preferredTheme = request.PreferredTheme }, JsonOptions);
        var generateRequest = new HttpRequestMessage(
            HttpMethod.Post, $"api/pipeline/{Uri.EscapeDataString(sessionId)}/generate");
        generateRequest.Headers.Add("X-Correlation-Id", correlationId);
        generateRequest.Content = new StringContent(generatePayload, System.Text.Encoding.UTF8, "application/json");

        HttpResponseMessage generateResponse;
        try
        {
            generateResponse = await _httpClient.SendAsync(generateRequest, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            _logger.LogError(
                "ReportDesigner.GenerateTimeout DurationMs={DurationMs} CorrelationId={CorrelationId}",
                sw.ElapsedMilliseconds, correlationId);
            throw new HttpRequestException(
                "Report Designer generate request timed out.", ex, HttpStatusCode.RequestTimeout);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            _logger.LogError(
                "ReportDesigner.GenerateFailed ExceptionType={ExceptionType} Message={Message} " +
                "DurationMs={DurationMs} CorrelationId={CorrelationId}",
                ex.GetType().Name, ex.Message, sw.ElapsedMilliseconds, correlationId);
            throw;
        }

        using (generateResponse)
        {
            sw.Stop();
            var generateBody = await generateResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!generateResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "ReportDesigner.GenerateFailed StatusCode={StatusCode} DurationMs={DurationMs} " +
                    "CorrelationId={CorrelationId}",
                    (int)generateResponse.StatusCode, sw.ElapsedMilliseconds, correlationId);

                throw new HttpRequestException(
                    MapStatusCode(generateResponse.StatusCode, generateBody),
                    inner: null,
                    statusCode: generateResponse.StatusCode);
            }

            JsonElement blueprint;
            try
            {
                using var doc = JsonDocument.Parse(generateBody);
                blueprint = doc.RootElement.Clone();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex,
                    "ReportDesigner.GenerateParseError CorrelationId={CorrelationId} Body={Body}",
                    correlationId, Truncate(generateBody, 1000));
                throw new InvalidOperationException(
                    "Report Designer generate response could not be parsed.", ex);
            }

            // ── AI boundary log — AFTER receiving ────────────────────────────
            // Logs only structural summary — no data values.
            _logger.LogInformation(
                "ReportDesigner.AIResponseReceived CorrelationId={CorrelationId} " +
                "SessionId={SessionId} DurationMs={DurationMs}",
                correlationId,
                sessionId,
                sw.ElapsedMilliseconds);

            return new GenerateReportModelResponse(
                CorrelationId: correlationId,
                DurationMs: sw.ElapsedMilliseconds,
                Blueprint: blueprint,
                SessionId: sessionId);
        }
    }

    private static string MapStatusCode(HttpStatusCode statusCode, string body) =>
        (int)statusCode switch
        {
            400 => "Report Designer AI rejected the schema payload.",
            401 => "Report Designer AI authentication failed. Verify TRANSFORMERS_AGENTS_API_KEY.",
            403 => "Report Designer AI API key rejected or insufficient permissions. Verify TRANSFORMERS_AGENTS_API_KEY.",
            404 => "Report Designer AI endpoint not found. Check REPORT_DESIGNER_BASE_URL.",
            429 => "Report Designer AI rate limit exceeded.",
            500 => "Report Designer AI internal error.",
            503 => "Report Designer AI unavailable.",
            _   => $"Report Designer AI returned {(int)statusCode}: {Truncate(body, 300)}"
        };

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : string.Concat(s.AsSpan(0, max), "…");

    private record PipelineConnectResponse(string SessionId);
}
