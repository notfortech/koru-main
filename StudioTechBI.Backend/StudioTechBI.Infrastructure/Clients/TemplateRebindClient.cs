using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.TemplateRefresh;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.Infrastructure.Clients;

/// <summary>
/// Calls stbi-bind-deploy's new POST /api/templates/rebind. Same request/logging/error-handling
/// shape as BindDeployClient — deliberately a separate class rather than adding a method to it,
/// so the existing DeployDatasetAsync path is untouched.
/// </summary>
public class TemplateRebindClient : ITemplateRebindClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<TemplateRebindClient> _logger;

    public TemplateRebindClient(HttpClient httpClient, ILogger<TemplateRebindClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<TemplateRebindResult> RebindAsync(
        TemplateRebindRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (_httpClient.BaseAddress is null)
            throw new InvalidOperationException(
                "TemplateRebindClient: HttpClient.BaseAddress is null. Verify BindDeploy:BaseUrl is configured.");

        _logger.LogInformation(
            "TemplateRebind.RebindRequested CorrelationId={CorrelationId} ClientName={ClientName} TemplateDatasetName={TemplateDatasetName}",
            correlationId, request.ClientName, request.TemplateDatasetName);

        var payload = JsonSerializer.Serialize(request, JsonOptions);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/templates/rebind");
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
                "TemplateRebind.RebindTimeout DurationMs={DurationMs} CorrelationId={CorrelationId}",
                sw.ElapsedMilliseconds, correlationId);
            throw new HttpRequestException(
                "Template rebind request timed out.", ex, HttpStatusCode.RequestTimeout);
        }

        using (response)
        {
            sw.Stop();
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "TemplateRebind.RebindFailed StatusCode={StatusCode} DurationMs={DurationMs} CorrelationId={CorrelationId} Body={Body}",
                    (int)response.StatusCode, sw.ElapsedMilliseconds, correlationId, Truncate(body, 500));
                throw new HttpRequestException(
                    $"Template rebind call failed: {(int)response.StatusCode} {Truncate(body, 300)}",
                    inner: null, statusCode: response.StatusCode);
            }

            TemplateRebindResult result;
            try
            {
                result = JsonSerializer.Deserialize<TemplateRebindResult>(body, JsonOptions)
                    ?? throw new InvalidOperationException("Template rebind returned an empty response body.");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex,
                    "TemplateRebind.RebindParseError CorrelationId={CorrelationId} Body={Body}",
                    correlationId, Truncate(body, 1000));
                throw new InvalidOperationException("Template rebind response could not be parsed.", ex);
            }

            _logger.LogInformation(
                "TemplateRebind.RebindCompleted CorrelationId={CorrelationId} WorkspaceId={WorkspaceId} DatasetId={DatasetId} DurationMs={DurationMs}",
                correlationId, result.WorkspaceId, result.DatasetId, sw.ElapsedMilliseconds);

            return result;
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : string.Concat(s.AsSpan(0, max), "…");
}
