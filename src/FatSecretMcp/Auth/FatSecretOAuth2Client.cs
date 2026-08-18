// SPDX-License-Identifier: MIT

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FatSecretMcp.Auth;

/// <summary>
/// OAuth 2.0 client-credentials flow for FatSecret's newer REST endpoints (autocomplete, barcode lookup,
/// and any other method that the classic OAuth1-signed server.api dispatch rejects with "Unknown method" -
/// those are only registered for Bearer-authenticated requests, regardless of OAuth1 scope granted).
/// See https://platform.fatsecret.com/docs/guides/authentication/oauth2.
///
/// Unverified against the live API as of writing: needs a real client id/secret from issue #3, which is a
/// manual app-registration step. Signature/response shapes are taken from FatSecret's docs, which have
/// already been wrong once elsewhere in this project (the OAuth1 request_token HTTP method) - treat this
/// as a first pass to be confirmed once credentials exist, not a proven implementation like the OAuth1 client.
/// </summary>
public sealed class FatSecretOAuth2Client(HttpClient httpClient, string clientId, string clientSecret)
{
    private const string TokenUrl = "https://oauth.fatsecret.com/connect/token";
    private const string ApiUrl = "https://platform.fatsecret.com/rest/server.api";

    private readonly Dictionary<string, (string Token, DateTimeOffset ExpiresAt)> _cachedTokens = new();

    public async Task<string> GetAccessTokenAsync(string scope, CancellationToken cancellationToken = default)
    {
        if (_cachedTokens.TryGetValue(scope, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return cached.Token;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl) {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> {
                ["grant_type"] = "client_credentials",
                ["scope"] = scope,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}")));

        var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"FatSecret OAuth2 token request failed ({(int)response.StatusCode}): {body}");
        }

        using var document = JsonDocument.Parse(body);
        var accessToken = document.RootElement.GetProperty("access_token").GetString()!;
        var expiresIn = document.RootElement.GetProperty("expires_in").GetInt32();

        _cachedTokens[scope] = (accessToken, DateTimeOffset.UtcNow.AddSeconds(expiresIn));
        return accessToken;
    }

    public async Task<string> CallApiAsync(
        string method,
        IReadOnlyDictionary<string, string> parameters,
        string scope,
        CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync(scope, cancellationToken);

        var formParams = new Dictionary<string, string>(parameters) {
            ["method"] = method,
            ["format"] = "json",
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl) {
            Content = new FormUrlEncodedContent(formParams),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"FatSecret request failed ({(int)response.StatusCode}): {body}");
        }

        return body;
    }
}
