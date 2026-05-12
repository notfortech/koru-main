namespace StudioTechBI.API.Integrations.Common;

/// <summary>Abstraction for OAuth (or similar) access tokens used by outbound integration HTTP clients (Domain, CoreLogic, etc.).</summary>
public interface IIntegrationAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>Clears any cached token so the next call fetches a new one (e.g. after HTTP 401 from resource API).</summary>
    void InvalidateCachedToken();
}
