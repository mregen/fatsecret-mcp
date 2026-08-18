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

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

app.MapMcp("/mcp");

app.Run();
