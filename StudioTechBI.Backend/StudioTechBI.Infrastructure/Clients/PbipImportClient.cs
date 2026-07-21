using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.BindDeploy;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.Infrastructure.Clients;

/// <summary>
/// Calls stbi-bind-deploy's new POST /api/deployments/pbip-import. Same request/logging/error
/// -handling shape as TemplateRebindClient — deliberately a separate class so the existing
/// DeployDatasetAsync and TemplateRebindClient.RebindAsync paths stay untouched. Import polling on
/// the server side can take up to ~60s on top of everything upstream, so this reuses the same long
/// HTTP timeout as the other BindDeploy clients (BindDeployOptions.TimeoutSeconds).
/// </summary>
public class PbipImportClient : IPbipImportClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<PbipImportClient> _logger;

    public PbipImportClient(HttpClient httpClient, ILogger<PbipImportClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<PbipImportResult> ImportAsync(
        PbipImportRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (_httpClient.BaseAddress is null)
            throw new InvalidOperationException(
                "PbipImportClient: HttpClient.BaseAddress is null. Verify BindDeploy:BaseUrl is configured.");

        _logger.LogInformation(
            "PbipImport.ImportRequested CorrelationId={CorrelationId} WorkspaceName={WorkspaceName} ReportName={ReportName} FileCount={FileCount}",
            correlationId, request.WorkspaceName, request.ReportName, request.Files.Count);

        var payload = JsonSerializer.Serialize(request, JsonOptions);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/deployments/pbip-import");
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
                "PbipImport.ImportTimeout DurationMs={DurationMs} CorrelationId={CorrelationId}",
                sw.ElapsedMilliseconds, correlationId);
            throw new HttpRequestException(
                "PBIP import request timed out.", ex, HttpStatusCode.RequestTimeout);
        }

        using (response)
        {
            sw.Stop();
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "PbipImport.ImportFailed StatusCode={StatusCode} DurationMs={DurationMs} CorrelationId={CorrelationId} Body={Body}",
                    (int)response.StatusCode, sw.ElapsedMilliseconds, correlationId, Truncate(body, 500));
                throw new HttpRequestException(
                    $"PBIP import call failed: {(int)response.StatusCode} {Truncate(body, 300)}",
                    inner: null, statusCode: response.StatusCode);
            }

            PbipImportResult result;
            try
            {
                result = JsonSerializer.Deserialize<PbipImportResult>(body, JsonOptions)
                    ?? throw new InvalidOperationException("PBIP import returned an empty response body.");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex,
                    "PbipImport.ImportParseError CorrelationId={CorrelationId} Body={Body}",
                    correlationId, Truncate(body, 1000));
                throw new InvalidOperationException("PBIP import response could not be parsed.", ex);
            }

            _logger.LogInformation(
                "PbipImport.ImportCompleted CorrelationId={CorrelationId} WorkspaceId={WorkspaceId} DatasetId={DatasetId} ReportId={ReportId} DurationMs={DurationMs}",
                correlationId, result.WorkspaceId, result.DatasetId, result.ReportId, sw.ElapsedMilliseconds);

            return result;
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : string.Concat(s.AsSpan(0, max), "…");
}
