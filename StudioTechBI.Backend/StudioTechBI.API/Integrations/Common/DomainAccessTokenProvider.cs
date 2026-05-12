using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StudioTechBI.API.Configuration;
using StudioTechBI.API.Models.Domain.Responses;

namespace StudioTechBI.API.Integrations.Common;

public static class DomainHttpClientNames
{
    public const string Auth = "DomainAuth";
    public const string ResourceApi = "DomainResourceApi";
}

/// <summary>Domain.com.au client-credentials token from <c>/v1/connect/token</c> on the auth host.</summary>
public sealed class DomainAccessTokenProvider : IIntegrationAccessTokenProvider
{
    public const string CacheKey = "DomainIntegration:OAuth:AccessToken";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<DomainApiSettings> _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DomainAccessTokenProvider> _logger;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public DomainAccessTokenProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<DomainApiSettings> options,
        IMemoryCache cache,
        ILogger<DomainAccessTokenProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _cache = cache;
        _logger = logger;
    }

    public void InvalidateCachedToken()
    {
        _cache.Remove(CacheKey);
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out string? cached) && !string.IsNullOrEmpty(cached))
            return cached;

        await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cache.TryGetValue(CacheKey, out cached) && !string.IsNullOrEmpty(cached))
                return cached;

            var settings = _options.Value;
            if (string.IsNullOrWhiteSpace(settings.ClientId) || string.IsNullOrWhiteSpace(settings.ClientSecret))
            {
                _logger.LogWarning("Domain API OAuth skipped: ClientId or ClientSecret is not configured.");
                throw new InvalidOperationException("Domain API credentials are not configured.");
            }

            var form = new List<KeyValuePair<string, string>>
            {
                new("grant_type", "client_credentials"),
                new("client_id", settings.ClientId),
                new("client_secret", settings.ClientSecret)
            };
            if (!string.IsNullOrWhiteSpace(settings.Scope))
                form.Add(new KeyValuePair<string, string>("scope", settings.Scope.Trim()));

            using var content = new FormUrlEncodedContent(form);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded")
            {
                CharSet = Encoding.UTF8.WebName
            };

            var httpClient = _httpClientFactory.CreateClient(DomainHttpClientNames.Auth);

            HttpResponseMessage response;
            try
            {
                response = await httpClient.PostAsync("v1/connect/token", content, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Domain OAuth token request timed out.");
                throw;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Domain OAuth token request failed (network).");
                throw;
            }

            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                DomainTokenErrorResponse? err = null;
                try
                {
                    err = JsonSerializer.Deserialize<DomainTokenErrorResponse>(payload, JsonOptions);
                }
                catch (JsonException)
                {
                    // ignore parse failure
                }

                var reason = err?.Error ?? response.StatusCode.ToString();
                _logger.LogWarning(
                    "Domain OAuth token request failed with status {Status}. Error: {Error}.",
                    (int)response.StatusCode,
                    reason);
                var desc = err?.ErrorDescription;
                throw new InvalidOperationException(
                    string.IsNullOrEmpty(desc)
                        ? $"Domain OAuth failed ({(int)response.StatusCode})."
                        : $"Domain OAuth failed ({(int)response.StatusCode}): {desc}");
            }

            var tokenResponse = JsonSerializer.Deserialize<DomainTokenResponse>(payload, JsonOptions);
            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                _logger.LogWarning("Domain OAuth returned success but no access_token in payload.");
                throw new InvalidOperationException("Domain OAuth returned an unexpected payload.");
            }

            var skewSeconds = 90;
            var ttlSeconds = Math.Max(60, tokenResponse.ExpiresIn - skewSeconds);
            _cache.Set(CacheKey, tokenResponse.AccessToken, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttlSeconds)
            });

            return tokenResponse.AccessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }
}
