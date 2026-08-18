using System.Security.Cryptography;
using System.Text;

namespace FatSecretMcp.Auth;

/// <summary>
/// HMAC-SHA1 request signing per OAuth 1.0a (RFC 5849). No .NET built-in support exists for this.
/// </summary>
public static class OAuth1Signer
{
    public static Dictionary<string, string> BuildBaseParams(string consumerKey, string? token = null)
    {
        var parameters = new Dictionary<string, string>
        {
            ["oauth_consumer_key"] = consumerKey,
            ["oauth_nonce"] = Guid.NewGuid().ToString("N"),
            ["oauth_signature_method"] = "HMAC-SHA1",
            ["oauth_timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
            ["oauth_version"] = "1.0",
        };

        if (token is not null)
        {
            parameters["oauth_token"] = token;
        }

        return parameters;
    }

    public static string Sign(
        string httpMethod,
        string baseUrl,
        IReadOnlyDictionary<string, string> parameters,
        string consumerSecret,
        string? tokenSecret = null)
    {
        var normalizedParams = string.Join('&',
            parameters
                .OrderBy(p => p.Key, StringComparer.Ordinal)
                .ThenBy(p => p.Value, StringComparer.Ordinal)
                .Select(p => $"{PercentEncode(p.Key)}={PercentEncode(p.Value)}"));

        var baseString = string.Join('&',
            httpMethod.ToUpperInvariant(),
            PercentEncode(baseUrl),
            PercentEncode(normalizedParams));

        var signingKey = $"{PercentEncode(consumerSecret)}&{PercentEncode(tokenSecret ?? string.Empty)}";

        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(signingKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// RFC 3986 percent-encoding. <see cref="Uri.EscapeDataString"/> alone is not sufficient:
    /// .NET deliberately leaves ! * ' ( ) unescaped for legacy reasons, but OAuth1 requires them encoded.
    /// </summary>
    public static string PercentEncode(string value) =>
        Uri.EscapeDataString(value)
            .Replace("!", "%21")
            .Replace("*", "%2A")
            .Replace("'", "%27")
            .Replace("(", "%28")
            .Replace(")", "%29");
}
