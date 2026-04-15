using Azure.Core;
using Microsoft.Graph;

namespace StudioTechBI.Infrastructure.Connectors;

/// <summary>Builds a <see cref="GraphServiceClient"/> for a pre-acquired delegated (or app) access token.</summary>
public sealed class MicrosoftGraphClientFactory
{
    /// <summary>
    /// Microsoft Graph .NET SDK v5 uses <see cref="TokenCredential"/> instead of v4's DelegateAuthenticationProvider.
    /// </summary>
    public GraphServiceClient Create(string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        var credential = new StaticAccessTokenCredential(accessToken.Trim());
        return new GraphServiceClient(credential);
    }

    private sealed class StaticAccessTokenCredential : TokenCredential
    {
        private readonly string _token;

        public StaticAccessTokenCredential(string token) => _token = token;

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new(_token, DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }
}
