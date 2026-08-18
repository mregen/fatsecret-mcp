// SPDX-License-Identifier: MIT

namespace FatSecretMcp.Auth;

/// <summary>
/// Thin wrapper around <see cref="FatSecretOAuth1Client"/> that supplies this server's stored
/// premier-scope access token, so individual tool classes don't each have to read it out of
/// configuration themselves.
/// </summary>
public sealed class FatSecretPremierApi(FatSecretOAuth1Client client, IConfiguration configuration)
{
    public Task<string> CallAsync(
        string method,
        IReadOnlyDictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var accessToken = configuration["FatSecret:OAuth1:AccessToken"]
            ?? throw new InvalidOperationException(
                "FatSecret:OAuth1:AccessToken is not configured. Run `dotnet run -- auth request` then `auth exchange` first.");
        var accessTokenSecret = configuration["FatSecret:OAuth1:AccessTokenSecret"]
            ?? throw new InvalidOperationException("FatSecret:OAuth1:AccessTokenSecret is not configured.");

        var methodParams = new Dictionary<string, string>(parameters ?? new Dictionary<string, string>()) {
            ["method"] = method,
            ["format"] = "json",
        };

        return client.CallApiAsync(methodParams, accessToken, accessTokenSecret, cancellationToken);
    }
}
