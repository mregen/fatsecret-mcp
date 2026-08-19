# fatsecret-mcp

A .NET-based Model Context Protocol (MCP) server for the [FatSecret Platform API](https://platform.fatsecret.com/docs/guides), installable as a .NET tool. Docker hosting is planned but deliberately deferred - see [Security](#security) below.

## Status

Functional prototype. Food diary, weight, and exercise tools work end to end against the real FatSecret API via OAuth 1.0a. Barcode lookup and autocomplete are implemented but not yet usable - they need a FatSecret app registered for OAuth 2.0 (see [issue #3](https://github.com/mregen/fatsecret-mcp/issues/3)). Food/recipe search isn't implemented yet ([issue #5](https://github.com/mregen/fatsecret-mcp/issues/5)). Not yet containerized - see [open issues](https://github.com/mregen/fatsecret-mcp/issues) for the current roadmap.

## Security

The HTTP transport has **no authentication or authorization layer yet** - anyone who can reach
the endpoint can call any tool, including the ones that read/write your real FatSecret data.
This is fine for local use (stdio, or `--http` left on `localhost`), but it means **this must
not be exposed publicly** - no public Docker hosting, no binding to `0.0.0.0` on an open network
- until that gap is closed. See [`docs/multi-tenant-cloud-service.md`](docs/multi-tenant-cloud-service.md)
for the auth work that's needed first (items #3/#4 there) and the reasoning behind deferring
containerized/cloud hosting.

## Available tools

| Tool | Auth needed | Notes |
|---|---|---|
| `echo` | none | Placeholder tool proving the transport works |
| `get_food_entries`, `add_food_entry`, `edit_food_entry`, `delete_food_entry` | OAuth 1.0a | Food diary CRUD |
| `get_weight_history`, `add_weight_entry` | OAuth 1.0a | Weight tracking |
| `get_exercise_entries`, `search_exercises`, `shift_exercise_time` | OAuth 1.0a | FatSecret models a day as a full 24-hour allocation across activities, not independent log entries - see the tool descriptions for how this works |
| `find_food_by_barcode`, `autocomplete_food` | OAuth 2.0 (not yet configured) | Implemented but unverified against the live API pending a real OAuth 2.0 app |

## Running locally

### Prerequisites

- .NET 10 SDK
- A FatSecret Platform API app - register one at https://platform.fatsecret.com/ if you don't have one

### 1. Configure credentials

Credentials are read from [.NET user-secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets), run from `src/FatSecretMcp/` - never commit them to the repo.

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

### 4. Point an MCP client at it

For Claude Code, stdio (default transport):

```bash
claude mcp add fatsecret-mcp -- dotnet run --project src/FatSecretMcp
```

Or HTTP, with the server already running from step 3:

```bash
claude mcp add --transport http fatsecret-local-dev http://localhost:5102/mcp
```

Then restart or reconnect your Claude Code session - new MCP registrations aren't picked up mid-session - and the tools above become available.

## Install as a .NET tool

Instead of running from source, package the server as an installable global tool:

```bash
dotnet pack src/FatSecretMcp/FatSecretMcp.csproj -c Release -o ./nupkg
dotnet tool install --global --add-source ./nupkg FatSecretMcp
```

This installs a `fatsecret-mcp` command (credentials/auth setup above still apply - it reads the same user-secrets). Run it the same way as `dotnet run`:

```bash
fatsecret-mcp                                       # stdio (default)
fatsecret-mcp --http --urls http://localhost:5102    # HTTP
fatsecret-mcp auth request                           # one-time OAuth1 flow
```

For Claude Code, point it at the installed command instead of `dotnet run`:

```bash
claude mcp add fatsecret-mcp -- fatsecret-mcp
```

## Publishing

`.github/workflows/build.yml` packs the tool (`.nupkg` + `.snupkg`) on every push to `main` and
uploads it as a workflow artifact - so a build is always inspectable, even though publishing to
nuget.org is currently **disabled** (`if: false` on the push step). This isn't a security
gate (the package contains no credentials - see [Security](#security) above for the actual
concern) so much as a "not polished enough yet" one: OAuth 2.0 is still unverified and food/recipe
search isn't implemented. To enable publishing once that's no longer true: add a `NUGET_API_KEY`
repository secret (from a nuget.org API key), then remove the `if: false` line.

## Plan / architecture

- Built on the official [ModelContextProtocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) (`ModelContextProtocol.AspNetCore`). Two transports, chosen at startup by branching on `args` before the host is built: stdio (default, `Host.CreateApplicationBuilder` + `.WithStdioServerTransport()`) or HTTP (`--http`, `WebApplication.CreateBuilder` + stateless Streamable HTTP via `MapMcp`) - the SDK doesn't support registering both on one builder, so `Program.cs` picks one.
- Also packable as a .NET global tool (`PackAsTool`, see above). Docker hosting is intentionally not built yet - see [Security](#security).
- Auth:
  - OAuth 1.0a 3-legged flow for the `premier` scope (user food diary, weight, exercise entries) - hand-rolled HMAC-SHA1 signing (`src/FatSecretMcp/Auth/`), no .NET or FatSecret SDK support for this exists.
  - OAuth 2.0 client-credentials flow for `basic`/`barcode` scope (food/recipe search, autocomplete, barcode lookup).
- Target: `net10.0`, single project for now.

## License

MIT — see [LICENSE](LICENSE).
