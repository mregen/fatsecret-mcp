// SPDX-License-Identifier: MIT

using FatSecretMcp.Auth;

if (args.Length > 0 && args[0] == "auth")
{
    var authBuilder = Host.CreateApplicationBuilder(args);
    // AddUserSecrets appends to the end of the provider chain, which would otherwise put it
    // *above* environment variables - re-adding env vars after restores the normal precedence
    // (env vars override user-secrets), so a client's env config can override a local secret.
    authBuilder.Configuration.AddUserSecrets<Program>();
    authBuilder.Configuration.AddEnvironmentVariables();
    await AuthCli.RunAsync(args[1..], authBuilder.Configuration);
    return;
}

var useHttp = args.Contains("--http");
var remainingArgs = args.Where(a => a != "--http").ToArray();

if (useHttp)
{
    var builder = WebApplication.CreateBuilder(remainingArgs);
    // AddUserSecrets appends to the end of the provider chain, which would otherwise put it
    // *above* environment variables - re-adding env vars after restores the normal precedence
    // (env vars override user-secrets), so a client's env config can override a local secret.
    builder.Configuration.AddUserSecrets<Program>();
    builder.Configuration.AddEnvironmentVariables();
    builder.Services.AddFatSecretClients();
    builder.Services
        .AddMcpServer()
        .WithHttpTransport()
        .WithToolsFromAssembly();

    var app = builder.Build();
    app.MapMcp("/mcp");
    app.Run();
}
else
{
    var builder = Host.CreateApplicationBuilder(remainingArgs);
    // AddUserSecrets appends to the end of the provider chain, which would otherwise put it
    // *above* environment variables - re-adding env vars after restores the normal precedence
    // (env vars override user-secrets), so a client's env config can override a local secret.
    builder.Configuration.AddUserSecrets<Program>();
    builder.Configuration.AddEnvironmentVariables();

    // Stdio is the JSON-RPC channel - any stray console log line on stdout would corrupt it.
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

    builder.Services.AddFatSecretClients();
    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly();

    await builder.Build().RunAsync();
}
