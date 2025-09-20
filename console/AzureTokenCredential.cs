using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;

/// <summary>
/// A minimal TokenCredential that reads a raw bearer token from the AZURE_TOKEN environment variable.
/// The token must be a valid OAuth2 access token for the requested resource scopes.
/// This credential ignores scopes and returns the same token with an expiration fixed at 24 hours from instance construction.
/// </summary>
public sealed class AzureTokenCredential : TokenCredential
{
    private const string EnvVarName = "AZURE_TOKEN";
    private readonly string _token;
    private readonly DateTimeOffset _expiresOn;

    public AzureTokenCredential()
    {
        var token = Environment.GetEnvironmentVariable(EnvVarName);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException($"Environment variable '{EnvVarName}' is not set or empty. Provide a valid bearer token.");
        }
        _token = token;
        _expiresOn = DateTimeOffset.UtcNow.AddHours(24);
    }

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        => new AccessToken(_token, _expiresOn);

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        => new ValueTask<AccessToken>(new AccessToken(_token, _expiresOn));
}
