using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.DTOs.Connectors;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.API.Controllers;

/// <summary>Browser OAuth redirects for Microsoft Graph–backed connectors (OneDrive).</summary>
[ApiController]
public class ConnectionsOAuthController : ControllerBase
{
    private const string OneDriveStateCachePrefix = "onedrive_oauth_state:";
    private static readonly string[] OneDriveScopes =
    [
        "offline_access",
        "Files.Read",
        "User.Read"
    ];

    private static readonly JsonSerializerOptions TokenJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IOptions<MicrosoftAuthOptions> _authOptions;
    private readonly IMemoryCache _cache;
    private readonly IDataConnectionService _dataConnectionService;
    private readonly IClientByCompanyQuery _clientByCompanyQuery;
    private readonly IClientService _clientService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ConnectionsOAuthController> _logger;

    public ConnectionsOAuthController(
        IOptions<MicrosoftAuthOptions> authOptions,
        IMemoryCache cache,
        IDataConnectionService dataConnectionService,
        IClientByCompanyQuery clientByCompanyQuery,
        IClientService clientService,
        IHttpClientFactory httpClientFactory,
        ILogger<ConnectionsOAuthController> logger)
    {
        _authOptions = authOptions;
        _cache = cache;
        _dataConnectionService = dataConnectionService;
        _clientByCompanyQuery = clientByCompanyQuery;
        _clientService = clientService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>Starts OAuth: redirects the browser to Microsoft login. Requires JWT and <c>clientId</c> query.</summary>
    [Authorize]
    [HttpGet("~/api/connections/oauth/onedrive")]
    public async Task<IActionResult> StartOneDriveOAuth([FromQuery] Guid clientId, CancellationToken cancellationToken)
    {
        if (clientId == Guid.Empty)
            return BadRequest(new { success = false, message = "clientId query parameter is required." });

        if (!await CanAccessClientAsync(clientId, cancellationToken))
        {
            _logger.LogWarning("User denied OneDrive OAuth start for client {ClientId}.", clientId);
            return StatusCode(403, new { success = false, message = "You do not have access to this client." });
        }

        var opt = _authOptions.Value;
        if (string.IsNullOrWhiteSpace(opt.ClientId) || string.IsNullOrWhiteSpace(opt.RedirectUri))
        {
            _logger.LogError("MicrosoftAuth:ClientId or MicrosoftAuth:RedirectUri is not configured.");
            return StatusCode(500, new { success = false, message = "Microsoft OAuth is not configured on the server." });
        }

        var state = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdRaw, out var userId))
            return Unauthorized(new { success = false, message = "Invalid user token." });

        _cache.Set(
            OneDriveStateCachePrefix + state,
            new OneDriveOAuthState(clientId, userId),
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) });

        var tenant = string.IsNullOrWhiteSpace(opt.TenantId) ? "common" : opt.TenantId.Trim();
        var authorizeUrl =
            $"https://login.microsoftonline.com/{Uri.EscapeDataString(tenant)}/oauth2/v2.0/authorize" +
            $"?client_id={Uri.EscapeDataString(opt.ClientId.Trim())}" +
            "&response_type=code" +
            $"&redirect_uri={Uri.EscapeDataString(opt.RedirectUri.Trim())}" +
            "&response_mode=query" +
            $"&scope={Uri.EscapeDataString(string.Join(' ', OneDriveScopes))}" +
            $"&state={Uri.EscapeDataString(state)}";

        _logger.LogInformation("Redirecting user {UserId} to Microsoft OAuth for client {ClientId}.", userId, clientId);
        return Redirect(authorizeUrl);
    }

    /// <summary>OAuth callback from Microsoft (no JWT). Exchanges code and registers a OneDrive <see cref="DataConnection"/>.</summary>
    [AllowAnonymous]
    [HttpGet("~/api/connections/oauth/onedrive/callback")]
    public async Task<IActionResult> OneDriveOAuthCallback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromQuery(Name = "error_description")] string? errorDescription,
        CancellationToken cancellationToken)
    {
        var opt = _authOptions.Value;

        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogWarning("OneDrive OAuth error from Microsoft: {Error} {Description}", error, errorDescription);
            return RedirectOrMessage(opt, success: false, message: $"Microsoft sign-in failed: {error}. {errorDescription}".Trim());
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            return BadRequest(new { success = false, message = "Missing code or state from OAuth callback." });

        if (!_cache.TryGetValue(OneDriveStateCachePrefix + state, out OneDriveOAuthState? oauthState) || oauthState == null)
        {
            _logger.LogWarning("OneDrive OAuth callback with unknown or expired state.");
            return BadRequest(new { success = false, message = "OAuth state is invalid or expired. Start the connection again." });
        }

        _cache.Remove(OneDriveStateCachePrefix + state);

        if (string.IsNullOrWhiteSpace(opt.ClientId)
            || string.IsNullOrWhiteSpace(opt.ClientSecret)
            || string.IsNullOrWhiteSpace(opt.RedirectUri))
        {
            _logger.LogError("Microsoft OAuth token exchange misconfiguration.");
            return StatusCode(500, new { success = false, message = "Microsoft OAuth is not fully configured on the server." });
        }

        var tenant = string.IsNullOrWhiteSpace(opt.TenantId) ? "common" : opt.TenantId.Trim();
        var tokenUrl = $"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token";

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var form = new Dictionary<string, string>
        {
            ["client_id"] = opt.ClientId.Trim(),
            ["client_secret"] = opt.ClientSecret.Trim(),
            ["grant_type"] = "authorization_code",
            ["code"] = code.Trim(),
            ["redirect_uri"] = opt.RedirectUri.Trim()
        };
        request.Content = new FormUrlEncodedContent(form);

        var http = _httpClientFactory.CreateClient();
        using var tokenResponse = await http.SendAsync(request, cancellationToken);
        var tokenBody = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            _logger.LogError(
                "OneDrive token exchange failed: {Status} {Body}",
                (int)tokenResponse.StatusCode,
                tokenBody.Length > 2000 ? tokenBody[..2000] : tokenBody);
            return RedirectOrMessage(opt, success: false, message: "Token exchange with Microsoft failed.");
        }

        MicrosoftOAuthTokenResponse? tokens;
        try
        {
            tokens = JsonSerializer.Deserialize<MicrosoftOAuthTokenResponse>(tokenBody, TokenJsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Microsoft token response.");
            return RedirectOrMessage(opt, success: false, message: "Invalid token response from Microsoft.");
        }

        if (string.IsNullOrWhiteSpace(tokens?.AccessToken))
            return RedirectOrMessage(opt, success: false, message: "Microsoft did not return an access token.");

        DateTime? expiresAt = null;
        if (tokens.ExpiresIn > 0)
            expiresAt = DateTime.UtcNow.AddSeconds(tokens.ExpiresIn);

        try
        {
            var dto = await _dataConnectionService.RegisterConnectionAsync(
                new RegisterDataConnectionDto
                {
                    ClientId = oauthState.ClientId,
                    Type = DataConnectionTypes.OneDrive,
                    AccessToken = tokens.AccessToken,
                    RefreshToken = tokens.RefreshToken,
                    ExpiresAt = expiresAt
                },
                cancellationToken);

            _logger.LogInformation(
                "Registered OneDrive data connection {ConnectionId} for client {ClientId} (OAuth user {UserId}).",
                dto.Id,
                oauthState.ClientId,
                oauthState.InitiatingUserId);

            return RedirectOrMessage(opt, success: true, message: null, connectionId: dto.Id);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Register OneDrive connection failed after OAuth.");
            return RedirectOrMessage(opt, success: false, message: ex.Message);
        }
    }

    private IActionResult RedirectOrMessage(MicrosoftAuthOptions opt, bool success, string? message, Guid? connectionId = null)
    {
        if (!string.IsNullOrWhiteSpace(opt.SuccessRedirectUri))
        {
            var baseUri = opt.SuccessRedirectUri.Trim();
            var q = new List<string>
            {
                "oauth=onedrive",
                success ? "success=1" : "success=0"
            };
            if (connectionId.HasValue)
                q.Add($"connectionId={Uri.EscapeDataString(connectionId.Value.ToString())}");
            if (!string.IsNullOrEmpty(message))
                q.Add($"message={Uri.EscapeDataString(message)}");

            var sep = baseUri.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            return Redirect($"{baseUri}{sep}{string.Join('&', q)}");
        }

        if (!success)
            return BadRequest(new { success = false, message });

        return Ok(new { success = true, connectionId, message = "OneDrive connected. You can close this window or return to the app." });
    }

    private async Task<IReadOnlyList<(string Code, Guid Id)>> GetAccessibleClientsAsync(CancellationToken ct)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email)) return Array.Empty<(string, Guid)>();

        var fromCompanies = await _clientByCompanyQuery.GetClientsForUserEmailAsync(email, ct);
        var list = fromCompanies.Select(c => (Code: c.ClientCode ?? "", c.ClientId)).Where(x => !string.IsNullOrEmpty(x.Code)).ToList();

        var claimCode = User.FindFirstValue("client_code");
        if (!string.IsNullOrEmpty(claimCode) && list.All(x => !string.Equals(x.Code, claimCode, StringComparison.OrdinalIgnoreCase)))
        {
            var fromClaim = await _clientService.GetByClientCodeOrIdAsync(claimCode, ct);
            if (fromClaim != null)
                list.Add((fromClaim.ClientCode ?? fromClaim.BlobFolderPath ?? claimCode, fromClaim.ClientId));
        }

        return list;
    }

    private async Task<bool> CanAccessClientAsync(Guid clientId, CancellationToken ct)
    {
        var accessible = await GetAccessibleClientsAsync(ct);
        return accessible.Any(a => a.Id == clientId);
    }

    private sealed record OneDriveOAuthState(Guid ClientId, Guid InitiatingUserId);
}

internal sealed class MicrosoftOAuthTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}
