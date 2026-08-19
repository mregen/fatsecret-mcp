# Developer guide

Notes for building, publishing, and understanding the internals of `fatsecret-mcp`. If you just
want to run the tool, see the main [README](../README.md) instead - nothing here is needed for
that.

## Architecture

- Built on the official [ModelContextProtocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) (`ModelContextProtocol.AspNetCore`). Two transports, chosen at startup by branching on `args` before the host is built: stdio (default, `Host.CreateApplicationBuilder` + `.WithStdioServerTransport()`) or HTTP (`--http`, `WebApplication.CreateBuilder` + stateless Streamable HTTP via `MapMcp`) - the SDK doesn't support registering both on one builder, so `Program.cs` picks one.
- Also packable as a .NET global tool (`PackAsTool`). Docker hosting is intentionally not built yet - see the [Security](../README.md#security) section in the README.
- Auth:
  - OAuth 1.0a 3-legged flow for the `premier` scope (user food diary, weight, exercise entries) - hand-rolled HMAC-SHA1 signing (`src/FatSecretMcp/Auth/`), no .NET or FatSecret SDK support for this exists.
  - OAuth 2.0 client-credentials flow for `basic`/`barcode` scope (food/recipe search, autocomplete, barcode lookup).
- Target: `net10.0`, single project for now.

See also [`multi-tenant-cloud-service.md`](multi-tenant-cloud-service.md) for the (not yet
started) sketch of what running this as a service for many end users would take.

## Running from source

The [README](../README.md) covers installing the published tool from nuget.org - this is for
working against a clone of this repo instead (e.g. to test unreleased changes).

### Prerequisites

- .NET 10 SDK
- A FatSecret Platform API app - register one at https://platform.fatsecret.com/ if you don't have one

### 1. Configure credentials

Credentials are read from [.NET user-secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets), run from `src/FatSecretMcp/` - never commit them to the repo. (The published tool instead uses environment variables, since a global tool install has no project directory to scope user-secrets to - see the README's [Configure credentials](../README.md#configure-credentials) section.)

For OAuth 1.0a (food diary, weight, exercise):

```bash
cd src/FatSecretMcp
dotnet user-secrets set "FatSecret:OAuth1:ConsumerKey" "<your consumer key>"
dotnet user-secrets set "FatSecret:OAuth1:ConsumerSecret" "<your consumer secret>"
```

For OAuth 2.0 (barcode lookup, autocomplete), once you have an app registered for it:

```bash
dotnet user-secrets set "FatSecret:OAuth2:ClientId" "<your client id>"
dotnet user-secrets set "FatSecret:OAuth2:ClientSecret" "<your client secret>"
```

### 2. Complete the OAuth 1.0a three-legged flow (one time)

This grants the server access to your own FatSecret account. It's interactive - you approve access in a browser - but doesn't need a callback server, since FatSecret shows a PIN you copy back in.

```bash
cd src/FatSecretMcp
dotnet run -- auth request
```

This prints a `token`, `token_secret`, and an `authorize_url`. Open the URL, log in, approve the app, and copy the PIN it shows you. Then:

```bash
dotnet run -- auth exchange <token> <token_secret> <pin>
```

This prints an `access_token` and `access_token_secret`. Store them:

```bash
dotnet user-secrets set "FatSecret:OAuth1:AccessToken" "<access_token>"
dotnet user-secrets set "FatSecret:OAuth1:AccessTokenSecret" "<access_token_secret>"
```

You only need to do this once - the access token doesn't expire on its own.

### 3. Run the server

The server supports two transports, chosen at startup - **stdio by default**, or HTTP via a flag:

```bash
cd src/FatSecretMcp

# stdio (default) - for MCP clients that spawn the process directly
dotnet run

# HTTP - a long-running server on a port, using ASP.NET Core's standard --urls flag
dotnet run -- --http --urls http://localhost:5102
```

In HTTP mode the MCP endpoint is at `<url>/mcp` (Streamable HTTP), e.g. `http://localhost:5102/mcp`.

**Never point a real MCP client at `dotnet run` for stdio** - its own "Building..." banner
pollutes stdout before the app starts, which corrupts the JSON-RPC channel. Use the built DLL
or an installed tool instead.

### 4. Point an MCP client at it

For Claude Code, stdio (default transport):

```bash
claude mcp add fatsecret-mcp -- dotnet run --project src/FatSecretMcp
```

Or HTTP, with the server already running from step 3:

```bash
claude mcp add --transport http fatsecret-local-dev http://localhost:5102/mcp
```

Then restart or reconnect your Claude Code session - new MCP registrations aren't picked up mid-session.

### Packing and installing a local build as a tool

To test the tool as it would actually be installed, without waiting on a NuGet.org publish:

```bash
dotnet pack src/FatSecretMcp/FatSecretMcp.csproj -c Release -o ./nupkg
dotnet tool install --global --add-source ./nupkg FatSecretMcp
```

This installs the same `fatsecret-mcp` command described in the README, from your local build.

## Publishing to NuGet.org

`.github/workflows/build.yml` packs the tool (`.nupkg` + `.snupkg`) on every push to `main` and
uploads it as a workflow artifact - so a build is always inspectable. Publishing to nuget.org is
a **separate, manual-only job** that never runs on a normal push/PR: it only exists on
`workflow_dispatch` (Actions tab → this workflow → **Run workflow**), gated behind a `publish`
checkbox input that **defaults to false**, and it `needs: build` so it can't run unless the
build+test job already succeeded.

Publishing uses nuget.org's [Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
(OIDC) instead of a stored API key - no long-lived secret in this repo at all. One-time setup,
on nuget.org (**username menu → Trusted Publishing → Add policy**):

| Field | Value |
|---|---|
| Repository Owner | `mregen` |
| Repository | `fatsecret-mcp` |
| Workflow File | `build.yml` |
| Environment | `nuget-publish` |

The `publish` job in the workflow also needs the real value filled in for `NuGet/login`'s
`user:` input (your nuget.org profile name, not email) - currently a `TODO_YOUR_NUGET_ORG_USERNAME`
placeholder.

After a successful push, the same job also creates a GitHub release (`gh release create`,
tagged `v<version>`) with the `.nupkg`/`.snupkg` attached and notes auto-generated from merged
PRs/commits since the last tag. The version is the one NBGV computed during `Pack`, extracted
from the packed filename (`FatSecretMcp.<version>.nupkg`) and passed between jobs via a job
`output`.

This isn't primarily a security gate (the package contains no credentials either way - see the
README's [Security](../README.md#security) section for the actual concern) so much as a "not
polished enough yet" one: OAuth 2.0 is still unverified and food/recipe search isn't
implemented. Trigger a manual publish run once that's no longer true.
