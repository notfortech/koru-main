using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.Exceptions;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Domain.Interfaces;

namespace StudioTechBI.Application.Services;

public class OAuthService : IOAuthService
{
    private static readonly TimeSpan DefaultStateTtl = TimeSpan.FromMinutes(10);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly string[] OneDriveScopes =
    [
        "offline_access",
        "Files.Read",
        "User.Read"
    ];

    private static readonly string[] GoogleScopes =
    [
        "https://www.googleapis.com/auth/drive.readonly",
        "https://www.googleapis.com/auth/userinfo.email",
        "https://www.googleapis.com/auth/userinfo.profile"
    ];

    private readonly IRepository<OAuthConnectionState> _stateRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<MicrosoftAuthOptions> _microsoftOptions;
    private readonly IOptions<GoogleAuthOptions> _googleOptions;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<OAuthService> _logger;

    public OAuthService(
        IRepository<OAuthConnectionState> stateRepo,
        IUnitOfWork unitOfWork,
        IHttpClientFactory httpClientFactory,
        IOptions<MicrosoftAuthOptions> microsoftOptions,
        IOptions<GoogleAuthOptions> googleOptions,
        IHostEnvironment hostEnvironment,
        ILogger<OAuthService> logger)
    {
        _stateRepo = stateRepo;
        _unitOfWork = unitOfWork;
        _httpClientFactory = httpClientFactory;
        _microsoftOptions = microsoftOptions;
        _googleOptions = googleOptions;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task<string> CreateAuthorizeUrlAsync(string provider, Guid clientId, Guid userId, CancellationToken cancellationToken = default)
    {
        var p = NormalizeProvider(provider);
        var state = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

        var now = DateTime.UtcNow;
        var entity = new OAuthConnectionState
        {
            Id = Guid.NewGuid(),
            Provider = p,
            State = state,
            ClientId = clientId,
            UserId = userId,
            ExpiresAtUtc = now.Add(DefaultStateTtl)
        };

        await _stateRepo.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return p switch
        {
            DataConnectionTypes.OneDrive => BuildMicrosoftAuthorizeUrl(state),
            DataConnectionTypes.GoogleDrive => BuildGoogleAuthorizeUrl(state),
            _ => throw new InvalidOperationException($"Unsupported OAuth provider '{p}'.")
        };
    }

    public async Task<(Guid ClientId, Guid UserId)> ConsumeStateAsync(string provider, string state, CancellationToken cancellationToken = default)
    {
        var p = NormalizeProvider(provider);
        var s = (state ?? "").Trim();
        if (s.Length == 0)
            throw new InvalidOperationException("Missing OAuth state.");

        var entity = await _stateRepo.FirstOrDefaultAsync(x => !x.IsDeleted && x.State == s, cancellationToken);
        if (entity == null)
            throw new InvalidOperationException("OAuth state is invalid or expired. Start the connection again.");

        if (!string.Equals(entity.Provider, p, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("OAuth state provider mismatch.");

        if (entity.ConsumedAtUtc.HasValue)
            throw new InvalidOperationException("OAuth state already used. Start the connection again.");

        if (entity.ExpiresAtUtc <= DateTime.UtcNow)
            throw new InvalidOperationException("OAuth state expired. Start the connection again.");

        entity.ConsumedAtUtc = DateTime.UtcNow;
        await _stateRepo.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (entity.ClientId, entity.UserId);
    }

    public async Task<(string AccessToken, string? RefreshToken, DateTime? ExpiresAtUtc)> ExchangeCodeForTokensAsync(
        string provider,
        string code,
        CancellationToken cancellationToken = default)
    {
        var p = NormalizeProvider(provider);
        var c = (code ?? "").Trim();
        if (c.Length == 0)
            throw new InvalidOperationException("Missing OAuth code.");

        return p switch
        {
            DataConnectionTypes.OneDrive => await ExchangeMicrosoftAsync(c, cancellationToken),
            DataConnectionTypes.GoogleDrive => await ExchangeGoogleAsync(c, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported OAuth provider '{p}'.")
        };
    }

    private string BuildMicrosoftAuthorizeUrl(string state)
    {
        var opt = _microsoftOptions.Value;
        if (string.IsNullOrWhiteSpace(opt.ClientId) || string.IsNullOrWhiteSpace(opt.RedirectUri))
            throw new InvalidOperationException("Microsoft OAuth is not configured.");

        var tenant = string.IsNullOrWhiteSpace(opt.TenantId) ? "common" : opt.TenantId.Trim();
        return
            $"https://login.microsoftonline.com/{Uri.EscapeDataString(tenant)}/oauth2/v2.0/authorize" +
            $"?client_id={Uri.EscapeDataString(opt.ClientId.Trim())}" +
            "&response_type=code" +
            $"&redirect_uri={Uri.EscapeDataString(opt.RedirectUri.Trim())}" +
            "&response_mode=query" +
            $"&scope={Uri.EscapeDataString(string.Join(' ', OneDriveScopes))}" +
            $"&state={Uri.EscapeDataString(state)}";
    }

    private string BuildGoogleAuthorizeUrl(string state)
    {
        var opt = _googleOptions.Value;
        EnsureGoogleOAuthConfigured(opt);

        EnsureGoogleProductionUriRules(opt);

        return
            "https://accounts.google.com/o/oauth2/v2/auth" +
            $"?client_id={Uri.EscapeDataString(opt.ClientId.Trim())}" +
            $"&redirect_uri={Uri.EscapeDataString(opt.RedirectUri.Trim())}" +
            "&response_type=code" +
            $"&scope={Uri.EscapeDataString(string.Join(' ', GoogleScopes))}" +
            "&access_type=offline" +
            "&prompt=consent" +
            $"&state={Uri.EscapeDataString(state)}";
    }

    /// <summary>Outside Development, require HTTPS and disallow localhost for Google redirect and success URLs.</summary>
    private void EnsureGoogleProductionUriRules(GoogleAuthOptions opt)
    {
        if (_hostEnvironment.IsDevelopment())
            return;

        static void RequireHttpsNoLocalhost(string name, string? uri)
        {
            if (string.IsNullOrWhiteSpace(uri))
                return;
            var u = uri.Trim();
            if (!u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                throw new OAuthConfigurationException($"{name} must use HTTPS in production.");
            if (u.Contains("localhost", StringComparison.OrdinalIgnoreCase))
                throw new OAuthConfigurationException($"{name} cannot use localhost in production.");
        }

        RequireHttpsNoLocalhost("Google OAuth RedirectUri", opt.RedirectUri);
        RequireHttpsNoLocalhost("Google OAuth SuccessRedirectUri", opt.SuccessRedirectUri);
    }

    /// <summary>Logs effective Google options (no secrets) and throws with a specific message if anything required is missing.</summary>
    private void EnsureGoogleOAuthConfigured(GoogleAuthOptions opt)
    {
        _logger.LogWarning(
            "Google Config Debug → ClientId: {ClientId}, RedirectUri: {RedirectUri}",
            opt.ClientId,
            opt.RedirectUri);

        if (string.IsNullOrWhiteSpace(opt.ClientId))
            throw new OAuthConfigurationException("Google ClientId is missing (check Azure env)");
        if (string.IsNullOrWhiteSpace(opt.ClientSecret))
            throw new OAuthConfigurationException("Google ClientSecret is missing (check Azure env)");
        if (string.IsNullOrWhiteSpace(opt.RedirectUri))
            throw new OAuthConfigurationException("Google RedirectUri is missing (check Azure env)");
    }

    private async Task<(string AccessToken, string? RefreshToken, DateTime? ExpiresAtUtc)> ExchangeMicrosoftAsync(string code, CancellationToken ct)
    {
        var opt = _microsoftOptions.Value;
        if (string.IsNullOrWhiteSpace(opt.ClientId)
            || string.IsNullOrWhiteSpace(opt.ClientSecret)
            || string.IsNullOrWhiteSpace(opt.RedirectUri))
            throw new InvalidOperationException("Microsoft OAuth is not fully configured.");

        var tenant = string.IsNullOrWhiteSpace(opt.TenantId) ? "common" : opt.TenantId.Trim();
        var tokenUrl = $"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token";

        using var req = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = opt.ClientId.Trim(),
            ["client_secret"] = opt.ClientSecret.Trim(),
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = opt.RedirectUri.Trim()
        });

        var http = _httpClientFactory.CreateClient();
        using var resp = await http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError("Microsoft token exchange failed: {Status} {Body}", (int)resp.StatusCode, body.Length > 2000 ? body[..2000] : body);
            throw new InvalidOperationException("Microsoft token exchange failed.");
        }

        var tokens = JsonSerializer.Deserialize<MicrosoftTokenResponse>(body, JsonOptions)
                     ?? throw new InvalidOperationException("Empty Microsoft token response.");
        if (string.IsNullOrWhiteSpace(tokens.AccessToken))
            throw new InvalidOperationException("Microsoft did not return an access token.");

        var expiresAt = tokens.ExpiresIn > 0 ? DateTime.UtcNow.AddSeconds(tokens.ExpiresIn) : (DateTime?)null;
        return (tokens.AccessToken!, tokens.RefreshToken, expiresAt);
    }

    private async Task<(string AccessToken, string? RefreshToken, DateTime? ExpiresAtUtc)> ExchangeGoogleAsync(string code, CancellationToken ct)
    {
        var opt = _googleOptions.Value;
        EnsureGoogleOAuthConfigured(opt);

        EnsureGoogleProductionUriRules(opt);

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token");
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = opt.ClientId.Trim(),
            ["client_secret"] = opt.ClientSecret.Trim(),
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = opt.RedirectUri.Trim()
        });

        var http = _httpClientFactory.CreateClient();
        using var resp = await http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError("Google token exchange failed: {Status} {Body}", (int)resp.StatusCode, body.Length > 2000 ? body[..2000] : body);
            throw new InvalidOperationException("Google token exchange failed.");
        }

        var tokens = JsonSerializer.Deserialize<GoogleTokenResponse>(body, JsonOptions)
                     ?? throw new InvalidOperationException("Empty Google token response.");
        if (string.IsNullOrWhiteSpace(tokens.AccessToken))
            throw new InvalidOperationException("Google did not return an access token.");

        var expiresAt = tokens.ExpiresIn > 0 ? DateTime.UtcNow.AddSeconds(tokens.ExpiresIn) : (DateTime?)null;
        return (tokens.AccessToken!, tokens.RefreshToken, expiresAt);
    }

    private static string NormalizeProvider(string provider)
    {
        var p = (provider ?? "").Trim();
        if (p.Length == 0) throw new InvalidOperationException("Provider is required.");

        // Accept friendly names too.
        if (string.Equals(p, "Google", StringComparison.OrdinalIgnoreCase)) return DataConnectionTypes.GoogleDrive;
        if (string.Equals(p, "GoogleDrive", StringComparison.OrdinalIgnoreCase)) return DataConnectionTypes.GoogleDrive;
        if (string.Equals(p, "OneDrive", StringComparison.OrdinalIgnoreCase)) return DataConnectionTypes.OneDrive;

        return p;
    }

    private sealed class MicrosoftTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    private sealed class GoogleTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}

