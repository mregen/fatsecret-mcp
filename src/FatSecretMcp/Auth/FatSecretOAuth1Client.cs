// SPDX-License-Identifier: MIT

namespace FatSecretMcp.Auth;

/// <summary>
/// OAuth 1.0a three-legged flow plus signed calls against the classic FatSecret REST API
/// (https://platform.fatsecret.com/docs/guides/authentication/oauth1/three-legged).
/// Works for both 2-legged (basic scope, no token) and 3-legged (premier scope, with access token) calls.
/// </summary>
public sealed class FatSecretOAuth1Client(HttpClient httpClient, string consumerKey, string consumerSecret)
{
    private const string RequestTokenUrl = "https://authentication.fatsecret.com/oauth/request_token";
    private const string AuthorizeUrl = "https://authentication.fatsecret.com/oauth/authorize";
    private const string AccessTokenUrl = "https://authentication.fatsecret.com/oauth/access_token";
    public const string ApiUrl = "https://platform.fatsecret.com/rest/server.api";

    public async Task<(string Token, string TokenSecret)> GetRequestTokenAsync(
        string callback = "oob", CancellationToken ct = default)
    {
        var oauthParams = OAuth1Signer.BuildBaseParams(consumerKey);
        oauthParams["oauth_callback"] = callback;
        oauthParams["oauth_signature"] = OAuth1Signer.Sign("GET", RequestTokenUrl, oauthParams, consumerSecret);

        var response = await httpClient.GetAsync($"{RequestTokenUrl}?{ToQueryString(oauthParams)}", ct);
        var parsed = ParseForm(await EnsureSuccessAsync(response, ct));

        if (parsed.GetValueOrDefault("oauth_callback_confirmed") != "true")
        {
            throw new InvalidOperationException("FatSecret did not confirm oauth_callback in the request token response.");
        }

        return (parsed["oauth_token"], parsed["oauth_token_secret"]);
    }

    public static string BuildAuthorizeUrl(string requestToken) =>
        $"{AuthorizeUrl}?oauth_token={OAuth1Signer.PercentEncode(requestToken)}";

    public async Task<(string AccessToken, string AccessTokenSecret)> GetAccessTokenAsync(
        string requestToken, string requestTokenSecret, string verifier, CancellationToken ct = default)
    {
        var oauthParams = OAuth1Signer.BuildBaseParams(consumerKey, requestToken);
        oauthParams["oauth_verifier"] = verifier;
        oauthParams["oauth_signature"] = OAuth1Signer.Sign(
            "GET", AccessTokenUrl, oauthParams, consumerSecret, requestTokenSecret);

        var response = await httpClient.GetAsync($"{AccessTokenUrl}?{ToQueryString(oauthParams)}", ct);
        var parsed = ParseForm(await EnsureSuccessAsync(response, ct));

        return (parsed["oauth_token"], parsed["oauth_token_secret"]);
    }

    /// <summary>
    /// Signs and issues a request against server.api. Omit accessToken/accessTokenSecret for a
    /// 2-legged (basic scope) call; supply them for a 3-legged (premier scope) call.
    /// </summary>
    public async Task<string> CallApiAsync(
        IReadOnlyDictionary<string, string> methodParams,
        string? accessToken = null,
        string? accessTokenSecret = null,
        CancellationToken ct = default)
    {
        var allParams = new Dictionary<string, string>(OAuth1Signer.BuildBaseParams(consumerKey, accessToken));
        foreach (var (key, value) in methodParams)
        {
            allParams[key] = value;
        }

        allParams["oauth_signature"] = OAuth1Signer.Sign("GET", ApiUrl, allParams, consumerSecret, accessTokenSecret);

        var response = await httpClient.GetAsync($"{ApiUrl}?{ToQueryString(allParams)}", ct);
        return await EnsureSuccessAsync(response, ct);
    }

    private static async Task<string> EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"FatSecret request failed ({(int)response.StatusCode}): {body}");
        }

        return body;
    }

    private static string ToQueryString(IReadOnlyDictionary<string, string> parameters) =>
        string.Join('&', parameters.Select(p => $"{OAuth1Signer.PercentEncode(p.Key)}={OAuth1Signer.PercentEncode(p.Value)}"));

    private static Dictionary<string, string> ParseForm(string body) =>
        body.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0]),
                parts => parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty);
}
