namespace FatSecretMcp.Auth;

/// <summary>
/// One-time interactive helper for completing the OAuth 1.0a three-legged flow by hand
/// (this is a personal single-user server, not a multi-tenant app — no callback listener needed
/// thanks to FatSecret's oob/PIN support).
///
/// Usage:
///   dotnet run -- auth request
///   dotnet run -- auth exchange &lt;token&gt; &lt;tokenSecret&gt; &lt;verifier&gt;
///   dotnet run -- auth call &lt;accessToken&gt; &lt;accessTokenSecret&gt; &lt;method&gt; [key=value ...]
/// </summary>
internal static class AuthCli
{
    public static async Task RunAsync(string[] args, IConfiguration configuration)
    {
        var consumerKey = configuration["FatSecret:OAuth1:ConsumerKey"]
            ?? throw new InvalidOperationException("FatSecret:OAuth1:ConsumerKey is not configured.");
        var consumerSecret = configuration["FatSecret:OAuth1:ConsumerSecret"]
            ?? throw new InvalidOperationException("FatSecret:OAuth1:ConsumerSecret is not configured.");

        using var httpClient = new HttpClient();
        var client = new FatSecretOAuth1Client(httpClient, consumerKey, consumerSecret);

        if (args.Length == 0)
        {
            Console.Error.WriteLine(
                "Usage: auth request | auth exchange <token> <tokenSecret> <verifier> | auth call <token> <tokenSecret> <method> [key=value ...]");
            return;
        }

        switch (args[0])
        {
            case "request":
            {
                var (token, tokenSecret) = await client.GetRequestTokenAsync();
                Console.WriteLine($"token={token}");
                Console.WriteLine($"token_secret={tokenSecret}");
                Console.WriteLine($"authorize_url={FatSecretOAuth1Client.BuildAuthorizeUrl(token)}");
                break;
            }

            case "exchange":
            {
                var (accessToken, accessTokenSecret) = await client.GetAccessTokenAsync(args[1], args[2], args[3]);
                Console.WriteLine($"access_token={accessToken}");
                Console.WriteLine($"access_token_secret={accessTokenSecret}");
                break;
            }

            case "call":
            {
                var methodParams = new Dictionary<string, string> { ["method"] = args[3], ["format"] = "json" };
                foreach (var kv in args.Skip(4))
                {
                    var parts = kv.Split('=', 2);
                    methodParams[parts[0]] = parts.Length > 1 ? parts[1] : string.Empty;
                }

                Console.WriteLine(await client.CallApiAsync(methodParams, args[1], args[2]));
                break;
            }

            default:
                Console.Error.WriteLine($"Unknown auth subcommand: {args[0]}");
                break;
        }
    }
}
