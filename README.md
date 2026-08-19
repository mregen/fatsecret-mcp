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

## Configure Claude Desktop, ChatGPT, or LM Studio

Do steps 1-2 above (credentials + the one-time OAuth1 browser flow) first - that part is always
interactive and can't happen from inside a client's spawned process.

**Can these clients start `fatsecret-mcp` automatically?** Claude Desktop and LM Studio: yes -
both spawn a local stdio process directly from their own config, no server to keep running
yourself. **ChatGPT: no** - it only connects to a reachable HTTPS URL, not a local command. See
its section below.

### Find the installed binary's absolute path

GUI-launched apps often don't inherit the PATH your terminal has, so the bare `fatsecret-mcp`
command name may not resolve even though it works in a shell. Use the absolute path instead:

- macOS/Linux: `~/.dotnet/tools/fatsecret-mcp`
- Windows: `%USERPROFILE%\.dotnet\tools\fatsecret-mcp.exe`

### Credentials as environment variables

Client configs pass credentials via an `env` object rather than `dotnet user-secrets` (which is
tied to this project's directory). .NET's config system maps environment variables using `__`
(double underscore) in place of the `:` used elsewhere in this README:

| user-secrets key | environment variable |
|---|---|
| `FatSecret:OAuth1:ConsumerKey` | `FatSecret__OAuth1__ConsumerKey` |
| `FatSecret:OAuth1:ConsumerSecret` | `FatSecret__OAuth1__ConsumerSecret` |
| `FatSecret:OAuth1:AccessToken` | `FatSecret__OAuth1__AccessToken` |
| `FatSecret:OAuth1:AccessTokenSecret` | `FatSecret__OAuth1__AccessTokenSecret` |
| `FatSecret:OAuth2:ClientId` (optional) | `FatSecret__OAuth2__ClientId` |
| `FatSecret:OAuth2:ClientSecret` (optional) | `FatSecret__OAuth2__ClientSecret` |

No code change needed for this - both hosts read environment variables automatically. If you
already ran the `dotnet user-secrets set` commands above on the same machine, `fatsecret-mcp`
picks those up regardless of how it's launched, and you can skip setting `env` entirely.

### Claude Desktop

Edit `claude_desktop_config.json` (macOS:
`~/Library/Application Support/Claude/claude_desktop_config.json`; Windows:
`%APPDATA%\Claude\claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "fatsecret-mcp": {
      "command": "/Users/you/.dotnet/tools/fatsecret-mcp",
      "args": [],
      "env": {
        "FatSecret__OAuth1__ConsumerKey": "<your consumer key>",
        "FatSecret__OAuth1__ConsumerSecret": "<your consumer secret>",
        "FatSecret__OAuth1__AccessToken": "<your access token>",
        "FatSecret__OAuth1__AccessTokenSecret": "<your access token secret>"
      }
    }
  }
}
```

Restart Claude Desktop to pick it up.

### LM Studio

LM Studio's MCP config (`mcp.json`) follows the same `command`/`args`/`env` shape as Claude
Desktop above. Its documented path is `~/.lmstudio/mcp.json` (macOS/Linux) /
`%USERPROFILE%\.lmstudio\mcp.json` (Windows), but there are user reports of the real path
differing by version/OS - rather than guessing, use the in-app editor: **Program tab → Install
→ Edit `mcp.json`**, which opens whichever file is actually authoritative for your install, and
paste in the same JSON shown for Claude Desktop above (just the inner object works too, since
LM Studio also uses an `mcpServers` map).

### ChatGPT

ChatGPT's connector model (Settings → Apps/Connectors → Developer mode → Advanced settings) only
accepts an HTTPS URL - it does not spawn local commands, so there's no direct equivalent to the
`command`/`args`/`env` config above. Two ways to actually reach it from ChatGPT, neither of them
a quick config edit:

- Run `fatsecret-mcp --http` and expose it over HTTPS - but per [Security](#security), that
  means anyone who can reach the URL gets full access to your FatSecret data, since there's no
  auth layer yet. Not recommended until that's built.
- OpenAI's [Secure MCP Tunnel](https://developers.openai.com/api/docs/guides/secure-mcp-tunnels)
  lets ChatGPT reach a local/private MCP server (stdio or HTTP) without exposing a public port,
  via a separate `tunnel-client` process you run alongside it. This project hasn't set that up
  or verified it works here - treat it as a starting point to investigate, not tested instructions.

## Contributing / developing

Building from source, architecture notes, and how NuGet publishing works are in
[`docs/DEVELOPER.md`](docs/DEVELOPER.md) - none of that is needed just to run the tool.

## License

MIT — see [LICENSE](LICENSE).
