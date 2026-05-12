using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using StudioTechBI.API.Integrations.Common;

namespace StudioTechBI.API.Services.Domain;

/// <summary>HTTP client for Domain resource API; uses <see cref="IIntegrationAccessTokenProvider"/> for bearer tokens.</summary>
public sealed class DomainPropertyLocationService : IDomainPropertyLocationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IIntegrationAccessTokenProvider _tokenProvider;
    private readonly ILogger<DomainPropertyLocationService> _logger;

    public DomainPropertyLocationService(
        HttpClient httpClient,
        IIntegrationAccessTokenProvider tokenProvider,
        ILogger<DomainPropertyLocationService> logger)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    public Task<object> GetAddressLocatorsAsync(string searchText, CancellationToken cancellationToken = default)
    {
        var path = QueryHelpers.AddQueryString("v1/addressLocators", "searchText", searchText ?? string.Empty);
        return GetJsonObjectAsync(path, cancellationToken);
    }

    public Task<object> GetDisclaimersAsync(CancellationToken cancellationToken = default) =>
        GetJsonObjectAsync("v1/disclaimers", cancellationToken);

    public Task<object> GetDisclaimersByProductAsync(string product, CancellationToken cancellationToken = default)
    {
        var p = Uri.EscapeDataString(product ?? string.Empty);
        return GetJsonObjectAsync($"v1/disclaimers/product/{p}", cancellationToken);
    }

    public Task<object> GetLocationProfileAsync(int domainLocationId, CancellationToken cancellationToken = default) =>
        GetJsonObjectAsync($"v1/locations/profiles/{domainLocationId}", cancellationToken);

    public Task<object> GetPropertyByIdAsync(long propertyId, CancellationToken cancellationToken = default) =>
        GetJsonObjectAsync($"v1/properties/{propertyId}", cancellationToken);

    public Task<object> GetSalesResultsHeadAsync(CancellationToken cancellationToken = default) =>
        GetJsonObjectAsync("v1/salesResults/_head", cancellationToken);

    public Task<object> GetSalesResultsByCityAsync(string city, CancellationToken cancellationToken = default)
    {
        var c = Uri.EscapeDataString(city ?? string.Empty);
        return GetJsonObjectAsync($"v1/salesResults/{c}", cancellationToken);
    }

    public Task<object> GetSalesResultsListingsAsync(string city, CancellationToken cancellationToken = default)
    {
        var c = Uri.EscapeDataString(city ?? string.Empty);
        return GetJsonObjectAsync($"v1/salesResults/{c}/listings", cancellationToken);
    }

    public Task<object> GetDemographicsAsync(string state, string suburb, string postcode, CancellationToken cancellationToken = default)
    {
        var path = $"v2/demographics/{Uri.EscapeDataString(state ?? string.Empty)}/{Uri.EscapeDataString(suburb ?? string.Empty)}/{Uri.EscapeDataString(postcode ?? string.Empty)}";
        return GetJsonObjectAsync(path, cancellationToken);
    }

    public Task<object> GetSuburbPerformanceStatisticsAsync(string state, string suburb, CancellationToken cancellationToken = default)
    {
        var path = $"v2/suburbPerformanceStatistics/{Uri.EscapeDataString(state ?? string.Empty)}/{Uri.EscapeDataString(suburb ?? string.Empty)}";
        return GetJsonObjectAsync(path, cancellationToken);
    }

    public Task<object> GetSuburbPerformanceStatisticsByPostcodeAsync(string state, string suburb, string postcode, CancellationToken cancellationToken = default)
    {
        var path = $"v2/suburbPerformanceStatistics/{Uri.EscapeDataString(state ?? string.Empty)}/{Uri.EscapeDataString(suburb ?? string.Empty)}/{Uri.EscapeDataString(postcode ?? string.Empty)}";
        return GetJsonObjectAsync(path, cancellationToken);
    }

    private async Task AddAuthHeaderAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<object> GetJsonObjectAsync(string relativePath, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, relativePath);
            await AddAuthHeaderAsync(request, cancellationToken).ConfigureAwait(false);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Domain API request timed out for {Path}.", relativePath);
                throw new DomainApiRequestException("The Domain API request timed out.", null, ex);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Domain API network error for {Path}.", relativePath);
                throw new DomainApiRequestException("The Domain API could not be reached.", null, ex);
            }

            using (response)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
                {
                    _logger.LogInformation("Domain API returned 401; invalidating cached token and retrying once for {Path}.", relativePath);
                    _tokenProvider.InvalidateCachedToken();
                    continue;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Domain API request failed for {Path} with status {Status}.",
                        relativePath,
                        (int)response.StatusCode);

                    throw new DomainApiRequestException(
                        $"Domain API returned {(int)response.StatusCode}.",
                        (int)response.StatusCode);
                }

                object data;
                try
                {
                    data = (await JsonSerializer.DeserializeAsync<JsonElement>(stream, JsonOptions, cancellationToken).ConfigureAwait(false))!;
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Domain API returned invalid JSON for {Path}.", relativePath);
                    throw new DomainApiRequestException("Domain API returned an unexpected response.", (int)response.StatusCode, ex);
                }

                // TODO: enqueue snapshot for Azure Functions → Azure SQL → Power BI semantic model
                return data;
            }
        }

        throw new InvalidOperationException("Unreachable: Domain API request loop exited without result.");
    }
}

