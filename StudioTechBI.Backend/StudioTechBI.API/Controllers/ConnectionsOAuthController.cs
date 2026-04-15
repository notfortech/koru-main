using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.Exceptions;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Application.Models;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.API.Controllers;

/// <summary>OAuth endpoints for external connector providers.</summary>
[ApiController]
public class ConnectionsOAuthController : ControllerBase
{
    private readonly IClientResolver _clientResolver;
    private readonly IOAuthService _oauthService;
    private readonly IDataConnectionService _dataConnectionService;
    private readonly IClientByCompanyQuery _clientByCompanyQuery;
    private readonly IClientService _clientService;
    private readonly IOptions<MicrosoftAuthOptions> _microsoftAuthOptions;
    private readonly IOptions<GoogleAuthOptions> _googleAuthOptions;
    private readonly ILogger<ConnectionsOAuthController> _logger;

    public ConnectionsOAuthController(
        IClientResolver clientResolver,
        IOAuthService oauthService,
        IDataConnectionService dataConnectionService,
        IClientByCompanyQuery clientByCompanyQuery,
        IClientService clientService,
        IOptions<MicrosoftAuthOptions> microsoftAuthOptions,
        IOptions<GoogleAuthOptions> googleAuthOptions,
        ILogger<ConnectionsOAuthController> logger)
    {
        _clientResolver = clientResolver;
        _oauthService = oauthService;
        _dataConnectionService = dataConnectionService;
        _clientByCompanyQuery = clientByCompanyQuery;
        _clientService = clientService;
        _microsoftAuthOptions = microsoftAuthOptions;
        _googleAuthOptions = googleAuthOptions;
        _logger = logger;
    }

    /// <summary>Starts OAuth: redirects the browser to Microsoft login. Requires JWT and <c>clientId</c> query (GUID or code like AU-004).</summary>
    [Authorize]
    [HttpGet("~/api/connections/oauth/onedrive")]
    public async Task<IActionResult> StartOneDriveOAuth([FromQuery] string clientId, CancellationToken cancellationToken)
    {
        var client = await ResolveClientAsync(clientId, cancellationToken);
        if (client == null)
            return NotFound(new { success = false, message = "Client not found." });

        if (!await CanAccessClientAsync(client.Id, cancellationToken))
            return StatusCode(403, new { success = false, message = "You do not have access to this client." });

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { success = false, message = "Invalid user token." });

        try
        {
            var authorizeUrl = await _oauthService.CreateAuthorizeUrlAsync(DataConnectionTypes.OneDrive, client.Id, userId, cancellationToken);
            _logger.LogInformation("Redirecting user {UserId} to OneDrive OAuth for client {ClientId}.", userId, client.Id);
            return Redirect(authorizeUrl);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "OneDrive OAuth is not configured.");
            return StatusCode(500, new { success = false, message = "Microsoft OAuth is not configured on the server." });
        }
    }

    /// <summary>
    /// Returns OAuth URL (no redirect) so the frontend can set <c>window.location.href</c>.
    /// Requires JWT and <c>clientId</c> query (GUID or code like AU-004).
    /// </summary>
    [Authorize]
    [HttpGet("~/api/connections/oauth/onedrive-url")]
    public async Task<IActionResult> GetOneDriveOAuthUrl([FromQuery] string clientId, CancellationToken cancellationToken)
    {
        var client = await ResolveClientAsync(clientId, cancellationToken);
        if (client == null)
            return NotFound(new { success = false, message = "Client not found." });

        if (!await CanAccessClientAsync(client.Id, cancellationToken))
            return StatusCode(403, new { success = false, message = "You do not have access to this client." });

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { success = false, message = "Invalid user token." });

        try
        {
            var url = await _oauthService.CreateAuthorizeUrlAsync(DataConnectionTypes.OneDrive, client.Id, userId, cancellationToken);
            return Ok(new { url });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "OneDrive OAuth is not configured.");
            return StatusCode(500, new { success = false, message = "Microsoft OAuth is not configured on the server." });
        }
    }

    /// <summary>Starts Google OAuth: redirects the browser to Google login. Requires JWT and <c>clientId</c> query (GUID or code like AU-004).</summary>
    [Authorize]
    [HttpGet("~/api/connections/oauth/google")]
    public async Task<IActionResult> StartGoogleOAuth([FromQuery] string clientId, CancellationToken cancellationToken)
    {
        var client = await ResolveClientAsync(clientId, cancellationToken);
        if (client == null)
            return NotFound(new { success = false, message = "Client not found." });

        if (!await CanAccessClientAsync(client.Id, cancellationToken))
            return StatusCode(403, new { success = false, message = "You do not have access to this client." });

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { success = false, message = "Invalid user token." });

        try
        {
            var url = await _oauthService.CreateAuthorizeUrlAsync(DataConnectionTypes.GoogleDrive, client.Id, userId, cancellationToken);
            _logger.LogInformation("Redirecting user {UserId} to Google OAuth for client {ClientId}.", userId, client.Id);
            return Redirect(url);
        }
        catch (OAuthConfigurationException ex)
        {
            _logger.LogWarning(ex, "Google OAuth is not configured for this environment.");
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Google OAuth failed.");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Returns Google OAuth URL (no redirect) so the frontend can set <c>window.location.href</c>.
    /// Requires JWT and <c>clientId</c> query (GUID or code like AU-004).
    /// </summary>
    [Authorize]
    [HttpGet("~/api/connections/oauth/google-url")]
    public async Task<IActionResult> GetGoogleOAuthUrl([FromQuery] string clientId, CancellationToken cancellationToken)
    {
        var client = await ResolveClientAsync(clientId, cancellationToken);
        if (client == null)
            return NotFound(new { success = false, message = "Client not found." });

        if (!await CanAccessClientAsync(client.Id, cancellationToken))
            return StatusCode(403, new { success = false, message = "You do not have access to this client." });

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { success = false, message = "Invalid user token." });

        try
        {
            var url = await _oauthService.CreateAuthorizeUrlAsync(DataConnectionTypes.GoogleDrive, client.Id, userId, cancellationToken);
            return Ok(new { url });
        }
        catch (OAuthConfigurationException ex)
        {
            _logger.LogWarning(ex, "Google OAuth is not configured for this environment.");
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Google OAuth failed.");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>OAuth callback from Google (no JWT). Exchanges code and registers a Google Drive <see cref="DataConnection"/>.</summary>
    [AllowAnonymous]
    [HttpGet("~/api/connections/oauth/google/callback")]
    public async Task<IActionResult> GoogleOAuthCallback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromQuery(Name = "error_description")] string? errorDescription,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogWarning("Google OAuth error: {Error} {Description}", error, errorDescription);
            return BadRequest(new { success = false, message = $"Google sign-in failed: {error}. {errorDescription}".Trim() });
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            return BadRequest(new { success = false, message = "Missing code or state from OAuth callback." });

        try
        {
            var (clientId, _) = await _oauthService.ConsumeStateAsync(DataConnectionTypes.GoogleDrive, state.Trim(), cancellationToken);
            var (accessToken, refreshToken, expiresAt) = await _oauthService.ExchangeCodeForTokensAsync(DataConnectionTypes.GoogleDrive, code.Trim(), cancellationToken);

            var dto = await _dataConnectionService.RegisterConnectionAsync(
                new StudioTechBI.Application.DTOs.Connectors.RegisterDataConnectionDto
                {
                    ClientId = clientId,
                    Type = DataConnectionTypes.GoogleDrive,
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresAt = expiresAt
                },
                cancellationToken);

            var target = AppendQuery(_googleAuthOptions.Value.SuccessRedirectUri, "connected", "google");
            return target != null
                ? Redirect(target)
                : Ok(new { success = true, connectionId = dto.Id, connected = "google" });
        }
        catch (OAuthConfigurationException ex)
        {
            _logger.LogWarning(ex, "Google OAuth configuration error on callback.");
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Google OAuth callback failed.");
            return BadRequest(new { success = false, message = ex.Message });
        }
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
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogWarning("OneDrive OAuth error from Microsoft: {Error} {Description}", error, errorDescription);
            return BadRequest(new { success = false, message = $"Microsoft sign-in failed: {error}. {errorDescription}".Trim() });
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            return BadRequest(new { success = false, message = "Missing code or state from OAuth callback." });

        try
        {
            var (clientId, _) = await _oauthService.ConsumeStateAsync(DataConnectionTypes.OneDrive, state.Trim(), cancellationToken);
            var (accessToken, refreshToken, expiresAt) = await _oauthService.ExchangeCodeForTokensAsync(DataConnectionTypes.OneDrive, code.Trim(), cancellationToken);

            var dto = await _dataConnectionService.RegisterConnectionAsync(
                new StudioTechBI.Application.DTOs.Connectors.RegisterDataConnectionDto
                {
                    ClientId = clientId,
                    Type = DataConnectionTypes.OneDrive,
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresAt = expiresAt
                },
                cancellationToken);

            var target = AppendQuery(_microsoftAuthOptions.Value.SuccessRedirectUri, "connected", "onedrive");
            return target != null
                ? Redirect(target)
                : Ok(new { success = true, connectionId = dto.Id, connected = "onedrive" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "OneDrive OAuth callback failed.");
            return BadRequest(new { success = false, message = ex.Message });
        }
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

    private bool TryGetUserId(out Guid userId)
    {
        var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdRaw, out userId);
    }

    private async Task<Client?> ResolveClientAsync(string clientIdOrCode, CancellationToken ct)
    {
        var value = (clientIdOrCode ?? "").Trim();
        if (value.Length == 0)
            return null;

        return await _clientResolver.ResolveAsync(value, ct);
    }

    private static string? AppendQuery(string? baseUrl, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return null;

        var u = baseUrl.Trim();
        var sep = u.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{u}{sep}{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";
    }
}
