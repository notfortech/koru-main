using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.DTOs.BindDeploy;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.Infrastructure.Clients;

public class BindDeployClient : IBindDeployClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<BindDeployClient> _logger;

    public BindDeployClient(HttpClient httpClient, ILogger<BindDeployClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<DeployDatasetResult> DeployDatasetAsync(
        DeployDatasetRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (_httpClient.BaseAddress is null)
            throw new InvalidOperationException(
                "BindDeployClient: HttpClient.BaseAddress is null. Verify BindDeploy:BaseUrl is configured.");

        _logger.LogInformation(
            "BindDeploy.DeployDatasetRequested CorrelationId={CorrelationId} ClientName={ClientName} DatasetName={DatasetName} FileCount={FileCount}",
            correlationId, request.ClientName, request.DatasetName, request.TmdlFiles.Count);

        var payload = JsonSerializer.Serialize(request, JsonOptions);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/deployments/dataset");
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
                "BindDeploy.DeployDatasetTimeout DurationMs={DurationMs} CorrelationId={CorrelationId}",
                sw.ElapsedMilliseconds, correlationId);
            throw new HttpRequestException(
                "Bind-deploy dataset request timed out.", ex, HttpStatusCode.RequestTimeout);
        }

        using (response)
        {
            sw.Stop();
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "BindDeploy.DeployDatasetFailed StatusCode={StatusCode} DurationMs={DurationMs} CorrelationId={CorrelationId} Body={Body}",
                    (int)response.StatusCode, sw.ElapsedMilliseconds, correlationId, Truncate(body, 500));
                throw new HttpRequestException(
                    $"Bind-deploy dataset call failed: {(int)response.StatusCode} {Truncate(body, 300)}",
                    inner: null, statusCode: response.StatusCode);
            }

            DeployDatasetResult result;
            try
            {
                result = JsonSerializer.Deserialize<DeployDatasetResult>(body, JsonOptions)
                    ?? throw new InvalidOperationException("Bind-deploy returned an empty response body.");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex,
                    "BindDeploy.DeployDatasetParseError CorrelationId={CorrelationId} Body={Body}",
                    correlationId, Truncate(body, 1000));
                throw new InvalidOperationException("Bind-deploy response could not be parsed.", ex);
            }

            _logger.LogInformation(
                "BindDeploy.DeployDatasetCompleted CorrelationId={CorrelationId} WorkspaceId={WorkspaceId} DatasetId={DatasetId} Created={Created} DurationMs={DurationMs}",
                correlationId, result.WorkspaceId, result.DatasetId, result.Created, sw.ElapsedMilliseconds);

            return result;
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : string.Concat(s.AsSpan(0, max), "…");
}
