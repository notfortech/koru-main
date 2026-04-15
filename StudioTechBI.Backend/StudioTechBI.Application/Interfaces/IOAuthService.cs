namespace StudioTechBI.Application.Interfaces;

public interface IOAuthService
{
    Task<string> CreateAuthorizeUrlAsync(
        string provider,
        Guid clientId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<(Guid ClientId, Guid UserId)> ConsumeStateAsync(
        string provider,
        string state,
        CancellationToken cancellationToken = default);

    Task<(string AccessToken, string? RefreshToken, DateTime? ExpiresAtUtc)> ExchangeCodeForTokensAsync(
        string provider,
        string code,
        CancellationToken cancellationToken = default);
}

