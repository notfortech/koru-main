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
    private readonly IAiBoundaryAuditService _auditService;

    public ReportDesignerClient(
        HttpClient httpClient,
        ILogger<ReportDesignerClient> logger,
        IOptions<ReportDesignerOptions> options,
        IAiBoundaryAuditService auditService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _opts = options.Value;
        _auditService = auditService;
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
            "TotalColumns={TotalColumns} PreferredTheme={PreferredTheme} AiProvider={AiProvider}",
            correlationId,
            request.Schema.Source,
            request.Schema.FileName,
            request.Schema.Tables.Count,
            string.Join(",", request.Schema.Tables.Select(t => t.TableName)),
            request.Schema.Tables.Sum(t => t.Columns.Count),
            request.PreferredTheme ?? "(none)",
            request.AiProvider ?? "(default)");

        var generateClientId = Guid.TryParse(request.ClientId, out var parsedGenerateClientId) ? parsedGenerateClientId : (Guid?)null;
        var sentMetadata = JsonSerializer.Serialize(new
        {
            source = request.Schema.Source,
            fileName = request.Schema.FileName,
            tableCount = request.Schema.Tables.Count,
            tableNames = request.Schema.Tables.Select(t => t.TableName),
            totalColumns = request.Schema.Tables.Sum(t => t.Columns.Count),
            preferredTheme = request.PreferredTheme
        }, JsonOptions);
        await _auditService.LogSentAsync(
            "ReportDesigner", "GenerateReportModel", correlationId, sentMetadata, generateClientId,
            cancellationToken: cancellationToken);

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // ── Step 1: Connect — send schema, receive sessionId ─────────────────
        // stbi_transformers' /connect endpoint natively expects a raw file upload or a
        // live DB connection string. koru-main already extracted structural metadata
        // upstream (excel/sql/sharepoint) and must never forward raw file bytes or
        // connection strings here, so we use its provider="schema" mode, which accepts
        // pre-extracted metadata directly and performs no data access of its own.
        var connectPayload = JsonSerializer.Serialize(BuildConnectPayload(request.Schema), JsonOptions);
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
            await _auditService.LogFailedAsync(
                "ReportDesigner", "GenerateReportModel", correlationId, sw.ElapsedMilliseconds,
                "Connect request timed out.", clientId: generateClientId, cancellationToken: cancellationToken);
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
            await _auditService.LogFailedAsync(
                "ReportDesigner", "GenerateReportModel", correlationId, sw.ElapsedMilliseconds,
                $"Connect failed: {ex.GetType().Name}", clientId: generateClientId, cancellationToken: cancellationToken);
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
                await _auditService.LogFailedAsync(
                    "ReportDesigner", "GenerateReportModel", correlationId, sw.ElapsedMilliseconds,
                    $"Connect failed with status {(int)connectResponse.StatusCode}",
                    (int)connectResponse.StatusCode, generateClientId, cancellationToken: cancellationToken);

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

        // ── Step 2: Designs — fetch the ranked template catalog for this session ──
        // Populates Templates on the response. Not fatal if it fails or returns
        // nothing — the frontend can still show the generated blueprint without
        // template recommendations.
        List<ReportTemplateRecommendation> templates = [];
        try
        {
            var designsRequest = new HttpRequestMessage(
                HttpMethod.Get, $"api/pipeline/{Uri.EscapeDataString(sessionId)}/designs");
            designsRequest.Headers.Add("X-Correlation-Id", correlationId);

            using var designsResponse = await _httpClient.SendAsync(designsRequest, cancellationToken);
            var designsBody = await designsResponse.Content.ReadAsStringAsync(cancellationToken);

            if (designsResponse.IsSuccessStatusCode)
            {
                var designOptions = JsonSerializer.Deserialize<List<DesignOptionDto>>(designsBody, JsonOptions) ?? [];
                templates = designOptions
                    .Select(d => new ReportTemplateRecommendation(d.TemplateId, d.Name, d.MatchScore))
                    .ToList();
            }
            else
            {
                _logger.LogWarning(
                    "ReportDesigner.DesignsFailed StatusCode={StatusCode} SessionId={SessionId} CorrelationId={CorrelationId}",
                    (int)designsResponse.StatusCode, sessionId, correlationId);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "ReportDesigner.DesignsFailed SessionId={SessionId} CorrelationId={CorrelationId} — continuing without templates",
                sessionId, correlationId);
        }

        // ── Step 3: Generate — send preferredTheme + aiProvider, receive blueprint ──
        // aiProvider ("anthropic" | "openai") lets the caller pick which LLM stbi_transformers
        // uses for this generation; null leaves stbi_transformers on its own default.
        var generatePayload = JsonSerializer.Serialize(
            new { preferredTheme = request.PreferredTheme, aiProvider = request.AiProvider }, JsonOptions);
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
            await _auditService.LogFailedAsync(
                "ReportDesigner", "GenerateReportModel", correlationId, sw.ElapsedMilliseconds,
                "Generate request timed out.", clientId: generateClientId, cancellationToken: cancellationToken);
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
            await _auditService.LogFailedAsync(
                "ReportDesigner", "GenerateReportModel", correlationId, sw.ElapsedMilliseconds,
                $"Generate failed: {ex.GetType().Name}", clientId: generateClientId, cancellationToken: cancellationToken);
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
                await _auditService.LogFailedAsync(
                    "ReportDesigner", "GenerateReportModel", correlationId, sw.ElapsedMilliseconds,
                    $"Generate failed with status {(int)generateResponse.StatusCode}",
                    (int)generateResponse.StatusCode, generateClientId, cancellationToken: cancellationToken);

                throw new HttpRequestException(
                    MapStatusCode(generateResponse.StatusCode, generateBody),
                    inner: null,
                    statusCode: generateResponse.StatusCode);
            }

            JsonElement blueprint;
            try
            {
                using var doc = JsonDocument.Parse(generateBody);
                // /generate returns the whole GenerateResponse envelope ({sessionId, blueprint,
                // bestTemplateMatch, confidence, generationTimeMs}), not the Blueprint itself —
                // unwrap it here so GenerateReportModelResponse.Blueprint (and ExtractStarSchema
                // below) get the actual blueprint object, matching what PublishReportRequest.Blueprint
                // expects when the frontend echoes it straight back to /publish.
                if (!doc.RootElement.TryGetProperty("blueprint", out var blueprintElement))
                    throw new InvalidOperationException(
                        "Report Designer generate response did not include a 'blueprint' field.");
                blueprint = blueprintElement.Clone();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex,
                    "ReportDesigner.GenerateParseError CorrelationId={CorrelationId} Body={Body}",
                    correlationId, Truncate(generateBody, 1000));
                await _auditService.LogFailedAsync(
                    "ReportDesigner", "GenerateReportModel", correlationId, sw.ElapsedMilliseconds,
                    "Generate response could not be parsed.", (int)generateResponse.StatusCode, generateClientId,
                    cancellationToken: cancellationToken);
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

            var receivedMetadata = JsonSerializer.Serialize(new
            {
                sessionId,
                templateCount = templates.Count
            }, JsonOptions);
            await _auditService.LogReceivedAsync(
                "ReportDesigner", "GenerateReportModel", correlationId, receivedMetadata,
                sw.ElapsedMilliseconds, (int)generateResponse.StatusCode, generateClientId,
                cancellationToken: cancellationToken);

            var starSchema = ExtractStarSchema(blueprint);

            return new GenerateReportModelResponse(
                CorrelationId: correlationId,
                DurationMs: sw.ElapsedMilliseconds,
                Blueprint: blueprint,
                SessionId: sessionId,
                StarSchema: starSchema,
                Templates: templates.Count > 0 ? templates : null);
        }
    }

    public async Task<SchemaModelAiMatchResponse> MatchSchemaModelAsync(
        SchemaModelAiMatchRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (_httpClient.BaseAddress is null)
            throw new InvalidOperationException(
                "ReportDesignerClient: HttpClient.BaseAddress is null. Verify ReportDesigner:BaseUrl is configured.");

        _logger.LogInformation(
            "ReportDesigner.SchemaModelMatchRequested CorrelationId={CorrelationId} ColumnCount={ColumnCount} CandidateCount={CandidateCount}",
            correlationId, request.Columns.Count, request.CandidateModels.Count);

        var matchSentMetadata = JsonSerializer.Serialize(new
        {
            columnCount = request.Columns.Count,
            candidateCount = request.CandidateModels.Count,
            candidateIds = request.CandidateModels.Select(c => c.Id)
        }, JsonOptions);
        await _auditService.LogSentAsync(
            "ReportDesigner", "MatchSchemaModel", correlationId, matchSentMetadata, cancellationToken: cancellationToken);

        var payload = JsonSerializer.Serialize(request, JsonOptions);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/schema-model-match");
        httpRequest.Headers.Add("X-Correlation-Id", correlationId);
        httpRequest.Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

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
                "ReportDesigner.SchemaModelMatchTimeout DurationMs={DurationMs} CorrelationId={CorrelationId}",
                sw.ElapsedMilliseconds, correlationId);
            await _auditService.LogFailedAsync(
                "ReportDesigner", "MatchSchemaModel", correlationId, sw.ElapsedMilliseconds,
                "Match request timed out.", cancellationToken: cancellationToken);
            throw new HttpRequestException(
                "Schema model match request timed out.", ex, HttpStatusCode.RequestTimeout);
        }

        using (response)
        {
            sw.Stop();
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "ReportDesigner.SchemaModelMatchFailed StatusCode={StatusCode} DurationMs={DurationMs} CorrelationId={CorrelationId}",
                    (int)response.StatusCode, sw.ElapsedMilliseconds, correlationId);
                await _auditService.LogFailedAsync(
                    "ReportDesigner", "MatchSchemaModel", correlationId, sw.ElapsedMilliseconds,
                    $"Match failed with status {(int)response.StatusCode}", (int)response.StatusCode,
                    cancellationToken: cancellationToken);
                throw new HttpRequestException(
                    MapStatusCode(response.StatusCode, body), inner: null, statusCode: response.StatusCode);
            }

            RawSchemaModelMatchResult raw;
            try
            {
                raw = JsonSerializer.Deserialize<RawSchemaModelMatchResult>(body, JsonOptions)
                    ?? throw new InvalidOperationException("Schema model match returned an empty response body.");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex,
                    "ReportDesigner.SchemaModelMatchParseError CorrelationId={CorrelationId} Body={Body}",
                    correlationId, Truncate(body, 1000));
                await _auditService.LogFailedAsync(
                    "ReportDesigner", "MatchSchemaModel", correlationId, sw.ElapsedMilliseconds,
                    "Match response could not be parsed.", (int)response.StatusCode, cancellationToken: cancellationToken);
                throw new InvalidOperationException("Schema model match response could not be parsed.", ex);
            }

            Guid? matchedModelId = null;
            if (!string.IsNullOrWhiteSpace(raw.MatchedModelId))
            {
                if (!Guid.TryParse(raw.MatchedModelId, out var parsed))
                    throw new InvalidOperationException($"Schema model match returned a non-guid matchedModelId '{raw.MatchedModelId}'.");
                matchedModelId = parsed;
            }

            _logger.LogInformation(
                "ReportDesigner.SchemaModelMatchSuccess CorrelationId={CorrelationId} MatchedModelId={MatchedModelId} " +
                "ProposedNew={ProposedNew} DurationMs={DurationMs} FieldMappingCount={FieldMappingCount}",
                correlationId, matchedModelId?.ToString() ?? "(none)", raw.ProposedModel is not null, sw.ElapsedMilliseconds,
                raw.FieldMappings?.Count ?? 0);

            var matchReceivedMetadata = JsonSerializer.Serialize(new
            {
                matchedModelId = matchedModelId?.ToString(),
                proposedNew = raw.ProposedModel is not null,
                confidence = raw.Confidence,
                fieldMappingCount = raw.FieldMappings?.Count ?? 0
            }, JsonOptions);
            await _auditService.LogReceivedAsync(
                "ReportDesigner", "MatchSchemaModel", correlationId, matchReceivedMetadata,
                sw.ElapsedMilliseconds, (int)response.StatusCode, cancellationToken: cancellationToken);

            return new SchemaModelAiMatchResponse(matchedModelId, raw.Confidence, raw.ProposedModel, raw.Reasoning, raw.FieldMappings);
        }
    }

    public async Task<AuthorTmdlResponse> AuthorTmdlAsync(
        JsonElement blueprint,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (_httpClient.BaseAddress is null)
            throw new InvalidOperationException(
                "ReportDesignerClient: HttpClient.BaseAddress is null. Verify ReportDesigner:BaseUrl is configured.");

        _logger.LogInformation("ReportDesigner.AuthorTmdlRequested CorrelationId={CorrelationId}", correlationId);

        await _auditService.LogSentAsync(
            "ReportDesigner", "AuthorTmdl", correlationId, "{}", cancellationToken: cancellationToken);

        var payload = JsonSerializer.Serialize(new { blueprint }, JsonOptions);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/blueprint/author-tmdl");
        httpRequest.Headers.Add("X-Correlation-Id", correlationId);
        httpRequest.Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

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
                "ReportDesigner.AuthorTmdlTimeout DurationMs={DurationMs} CorrelationId={CorrelationId}",
                sw.ElapsedMilliseconds, correlationId);
            await _auditService.LogFailedAsync(
                "ReportDesigner", "AuthorTmdl", correlationId, sw.ElapsedMilliseconds,
                "TMDL authoring request timed out.", cancellationToken: cancellationToken);
            throw new HttpRequestException(
                "Report Designer TMDL authoring request timed out.", ex, HttpStatusCode.RequestTimeout);
        }

        using (response)
        {
            sw.Stop();
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            // 422 is a real, meaningful response here (deterministic validation failed) — not a
            // transport failure — so it's parsed rather than thrown on the status code alone.
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.UnprocessableEntity)
            {
                _logger.LogWarning(
                    "ReportDesigner.AuthorTmdlFailed StatusCode={StatusCode} DurationMs={DurationMs} CorrelationId={CorrelationId}",
                    (int)response.StatusCode, sw.ElapsedMilliseconds, correlationId);
                await _auditService.LogFailedAsync(
                    "ReportDesigner", "AuthorTmdl", correlationId, sw.ElapsedMilliseconds,
                    $"AuthorTmdl failed with status {(int)response.StatusCode}", (int)response.StatusCode,
                    cancellationToken: cancellationToken);
                throw new HttpRequestException(
                    MapStatusCode(response.StatusCode, body), inner: null, statusCode: response.StatusCode);
            }

            AuthorTmdlResponse result;
            try
            {
                result = JsonSerializer.Deserialize<AuthorTmdlResponse>(body, JsonOptions)
                    ?? throw new InvalidOperationException("TMDL authoring returned an empty response body.");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex,
                    "ReportDesigner.AuthorTmdlParseError CorrelationId={CorrelationId} Body={Body}",
                    correlationId, Truncate(body, 1000));
                await _auditService.LogFailedAsync(
                    "ReportDesigner", "AuthorTmdl", correlationId, sw.ElapsedMilliseconds,
                    "AuthorTmdl response could not be parsed.", (int)response.StatusCode, cancellationToken: cancellationToken);
                throw new InvalidOperationException("TMDL authoring response could not be parsed.", ex);
            }

            _logger.LogInformation(
                "ReportDesigner.AuthorTmdlCompleted CorrelationId={CorrelationId} FileCount={FileCount} IsValid={IsValid} DurationMs={DurationMs}",
                correlationId, result.Files.Count, result.Validation?.IsValid, sw.ElapsedMilliseconds);

            var receivedMetadata = JsonSerializer.Serialize(new
            {
                fileCount = result.Files.Count,
                isValid = result.Validation?.IsValid,
                violationCount = result.Validation?.Violations.Count ?? 0
            }, JsonOptions);
            await _auditService.LogReceivedAsync(
                "ReportDesigner", "AuthorTmdl", correlationId, receivedMetadata,
                sw.ElapsedMilliseconds, (int)response.StatusCode, cancellationToken: cancellationToken);

            return result;
        }
    }

    private record RawSchemaModelMatchResult(
        string? MatchedModelId,
        double Confidence,
        ProposedSchemaModelDto? ProposedModel,
        string? Reasoning,
        List<SchemaModelFieldMatchDto>? FieldMappings);

    /// <summary>
    /// Derives the star-schema summary from the blueprint's own data_model section
    /// (stbi_transformers doesn't return a separate star-schema payload — the same
    /// fact/dimension/relationship data the frontend wants already lives inside the
    /// generated blueprint). Returns null if data_model is absent or has no fact table.
    /// </summary>
    private static StarSchemaDto? ExtractStarSchema(JsonElement blueprint)
    {
        if (!blueprint.TryGetProperty("data_model", out var dataModel))
            return null;

        if (!dataModel.TryGetProperty("fact_tables", out var factTables)
            || factTables.ValueKind != JsonValueKind.Array
            || factTables.GetArrayLength() == 0)
            return null;

        var factTableName = factTables[0].TryGetProperty("name", out var nameEl)
            ? nameEl.GetString() ?? string.Empty
            : string.Empty;

        var dimensionTables = new List<string>();
        if (dataModel.TryGetProperty("dimension_tables", out var dims) && dims.ValueKind == JsonValueKind.Array)
        {
            foreach (var dim in dims.EnumerateArray())
            {
                if (dim.TryGetProperty("name", out var dimName) && dimName.GetString() is { } n)
                    dimensionTables.Add(n);
            }
        }

        var relationships = new List<RelationshipDto>();
        if (dataModel.TryGetProperty("relationships", out var rels) && rels.ValueKind == JsonValueKind.Array)
        {
            foreach (var rel in rels.EnumerateArray())
            {
                var from = rel.TryGetProperty("from", out var f) ? f.GetString() : null;
                var to = rel.TryGetProperty("to", out var t) ? t.GetString() : null;
                if (from is null || to is null) continue;

                var (fromTable, fromColumn) = SplitTableColumn(from);
                var (toTable, toColumn) = SplitTableColumn(to);
                relationships.Add(new RelationshipDto(fromTable, fromColumn, toTable, toColumn));
            }
        }

        return new StarSchemaDto(factTableName, dimensionTables, relationships);
    }

    /// <summary>
    /// Maps koru-main's already-extracted schema into stbi_transformers' provider="schema"
    /// connect contract. No file content or connection details are ever included here —
    /// only table/column structural metadata.
    /// </summary>
    private static object BuildConnectPayload(ExtractedSchemaDto schema) => new
    {
        provider = "schema",
        fileName = schema.FileName,
        schema = new
        {
            databaseName = schema.FileName,
            tables = schema.Tables.Select(t => new
            {
                tableName = t.TableName,
                approximateRowCount = t.RowCount > 0 ? (long?)t.RowCount : null,
                columns = t.Columns.Select(c => new
                {
                    columnName = c.ColumnName,
                    dataType = c.DataType,
                    isNullable = c.IsNullable
                })
            })
        }
    };

    /// <summary>Parses "TableName[ColumnName]" into ("TableName", "ColumnName").</summary>
    private static (string Table, string Column) SplitTableColumn(string reference)
    {
        var openBracket = reference.IndexOf('[');
        var closeBracket = reference.IndexOf(']');
        if (openBracket < 0 || closeBracket <= openBracket)
            return (reference, string.Empty);

        return (reference[..openBracket], reference[(openBracket + 1)..closeBracket]);
    }

    private sealed record DesignOptionDto(string TemplateId, string Name, double MatchScore);

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
