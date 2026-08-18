// SPDX-License-Identifier: MIT

using FatSecretMcp.Auth;

var builder = WebApplication.CreateBuilder(args);

if (args.Length > 0 && args[0] == "auth")
{
    await AuthCli.RunAsync(args[1..], builder.Configuration);
    return;
}

builder.Services.AddHttpClient();
builder.Services.AddSingleton(sp => {
    var config = sp.GetRequiredService<IConfiguration>();
    var consumerKey = config["FatSecret:OAuth1:ConsumerKey"]
        ?? throw new InvalidOperationException("FatSecret:OAuth1:ConsumerKey is not configured.");
    var consumerSecret = config["FatSecret:OAuth1:ConsumerSecret"]
        ?? throw new InvalidOperationException("FatSecret:OAuth1:ConsumerSecret is not configured.");
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(FatSecretOAuth1Client));
    return new FatSecretOAuth1Client(httpClient, consumerKey, consumerSecret);
});
builder.Services.AddSingleton<FatSecretPremierApi>();
builder.Services.AddSingleton(sp => {
    var config = sp.GetRequiredService<IConfiguration>();
    var clientId = config["FatSecret:OAuth2:ClientId"]
        ?? throw new InvalidOperationException("FatSecret:OAuth2:ClientId is not configured.");
    var clientSecret = config["FatSecret:OAuth2:ClientSecret"]
        ?? throw new InvalidOperationException("FatSecret:OAuth2:ClientSecret is not configured.");
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(FatSecretOAuth2Client));
    return new FatSecretOAuth2Client(httpClient, clientId, clientSecret);
});

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

app.MapMcp("/mcp");

app.Run();
