// SPDX-License-Identifier: MIT

using FatSecretMcp.Auth;

var builder = WebApplication.CreateBuilder(args);

if (args.Length > 0 && args[0] == "auth")
{
    await AuthCli.RunAsync(args[1..], builder.Configuration);
    return;
}

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

app.MapMcp("/mcp");

app.Run();
