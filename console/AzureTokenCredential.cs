using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using System.Linq;

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
        token = token.Trim();
        if (token.IndexOf('\r') >= 0 || token.IndexOf('\n') >= 0)
        {
            throw new InvalidOperationException("AZURE_TOKEN contains newline characters which would corrupt the Authorization header.");
        }
        // Warn (but do not fail) if token does not look like a JWT (three segments)
        int dotCount = token.Count(c => c == '.');
        if (dotCount != 2)
        {
            // This could still be a valid opaque token; just emit a diagnostic to stderr
            Console.Error.WriteLine("[AzureTokenCredential] Warning: AZURE_TOKEN does not appear to be a standard JWT (expected 2 dots). Ensure it is a valid ARM access token.");
        }
        _token = token;
        _expiresOn = DateTimeOffset.UtcNow.AddHours(24);
    }

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        => new AccessToken(_token, _expiresOn);

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        => new ValueTask<AccessToken>(new AccessToken(_token, _expiresOn));
}
