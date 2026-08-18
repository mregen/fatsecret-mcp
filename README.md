# fatsecret-mcp

A .NET-based Model Context Protocol (MCP) server for the [FatSecret Platform API](https://platform.fatsecret.com/docs/guides), hostable as a Docker container or as a dotnet tool.

## Status

Functional prototype. Food diary, weight, and exercise tools work end to end against the real FatSecret API via OAuth 1.0a. Barcode lookup and autocomplete are implemented but not yet usable - they need a FatSecret app registered for OAuth 2.0 (see [issue #3](https://github.com/mregen/fatsecret-mcp/issues/3)). Food/recipe search isn't implemented yet ([issue #5](https://github.com/mregen/fatsecret-mcp/issues/5)). Not yet containerized - see [open issues](https://github.com/mregen/fatsecret-mcp/issues) for the current roadmap.

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
ASPNETCORE_ENVIRONMENT=Development dotnet run -- auth request
```

This prints a `token`, `token_secret`, and an `authorize_url`. Open the URL, log in, approve the app, and copy the PIN it shows you. Then:

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run -- auth exchange <token> <token_secret> <pin>
```

This prints an `access_token` and `access_token_secret`. Store them:

```bash
dotnet user-secrets set "FatSecret:OAuth1:AccessToken" "<access_token>"
dotnet user-secrets set "FatSecret:OAuth1:AccessTokenSecret" "<access_token_secret>"
```

You only need to do this once - the access token doesn't expire on its own.

### 3. Run the server

```bash
cd src/FatSecretMcp
ASPNETCORE_ENVIRONMENT=Development dotnet run --launch-profile http
```

The server listens on `http://localhost:5102`, with the MCP endpoint at `http://localhost:5102/mcp` (Streamable HTTP).

### 4. Point an MCP client at it

For Claude Code:

```bash
claude mcp add --transport http fatsecret-local-dev http://localhost:5102/mcp
```

Then restart or reconnect your Claude Code session - new MCP registrations aren't picked up mid-session - and the tools above become available.

## Plan / architecture

- Built on the official [ModelContextProtocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) (`ModelContextProtocol.AspNetCore`), using stateless Streamable HTTP transport (`MapMcp`).
- Auth:
  - OAuth 1.0a 3-legged flow for the `premier` scope (user food diary, weight, exercise entries) - hand-rolled HMAC-SHA1 signing (`src/FatSecretMcp/Auth/`), no .NET or FatSecret SDK support for this exists.
  - OAuth 2.0 client-credentials flow for `basic`/`barcode` scope (food/recipe search, autocomplete, barcode lookup).
- Target: `net10.0`, single project for now, no container yet.

## License

MIT — see [LICENSE](LICENSE).
