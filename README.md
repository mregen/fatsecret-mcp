# fatsecret-mcp

A .NET-based Model Context Protocol (MCP) server for the [FatSecret Platform API](https://platform.fatsecret.com/docs/guides), installable as a .NET tool from [nuget.org](https://www.nuget.org/packages/FatSecretMcp). Docker hosting is planned but deliberately deferred - see [Security](#security) below.

## Status

Functional prototype. Food diary, weight, and exercise tools work end to end against the real FatSecret API via OAuth 1.0a. Barcode lookup and autocomplete are implemented but not yet usable - they need a FatSecret app registered for OAuth 2.0 (see [issue #3](https://github.com/mregen/fatsecret-mcp/issues/3)). Food/recipe search isn't implemented yet ([issue #5](https://github.com/mregen/fatsecret-mcp/issues/5)). Not yet containerized - see [open issues](https://github.com/mregen/fatsecret-mcp/issues) for the current roadmap.

## Security

The HTTP transport has **no authentication or authorization layer yet** - anyone who can reach
the endpoint can call any tool, including the ones that read/write your real FatSecret data.
This is fine for local use (stdio, or `--http` left on `localhost`), but it means **this must
not be exposed publicly** - no public Docker hosting, no binding to `0.0.0.0` on an open network
- until that gap is closed. See [`docs/multi-tenant-cloud-service.md`](https://github.com/mregen/fatsecret-mcp/blob/main/docs/multi-tenant-cloud-service.md)
for the auth work that's needed first and the reasoning behind deferring containerized/cloud hosting.

## Available tools

| Tool | Auth needed | Notes |
|---|---|---|
| `echo` | none | Placeholder tool proving the transport works |
| `get_food_entries`, `add_food_entry`, `edit_food_entry`, `delete_food_entry` | OAuth 1.0a | Food diary CRUD |
| `get_weight_history`, `add_weight_entry` | OAuth 1.0a | Weight tracking |
| `get_exercise_entries`, `search_exercises`, `shift_exercise_time` | OAuth 1.0a | FatSecret models a day as a full 24-hour allocation across activities, not independent log entries - see the tool descriptions for how this works |
| `find_food_by_barcode`, `autocomplete_food` | OAuth 2.0 (not yet configured) | Implemented but unverified against the live API pending a real OAuth 2.0 app |

## Install

Requires the .NET 8 or .NET 10 SDK - the package multi-targets both, so `dotnet tool install`
picks whichever one matches your installed SDK automatically.

```bash
dotnet tool install --global FatSecretMcp
```

This installs a `fatsecret-mcp` command. Confirm it's on your PATH with `fatsecret-mcp --version`
(the .NET tools directory, `~/.dotnet/tools`, needs to be there - the installer usually adds it
automatically).

You'll also need a FatSecret Platform API app - register one at https://platform.fatsecret.com/
if you don't have one.

## Configure credentials

Credentials are passed as environment variables - `FatSecret:OAuth1:ConsumerKey` etc. become
`FatSecret__OAuth1__ConsumerKey` (double underscore in place of `:`), which is how .NET's config
system maps env vars automatically. No code or config file needed.

| Setting | Environment variable |
|---|---|
| OAuth 1.0a consumer key | `FatSecret__OAuth1__ConsumerKey` |
| OAuth 1.0a consumer secret | `FatSecret__OAuth1__ConsumerSecret` |
| OAuth 1.0a access token | `FatSecret__OAuth1__AccessToken` |
| OAuth 1.0a access token secret | `FatSecret__OAuth1__AccessTokenSecret` |
| OAuth 2.0 client id (optional) | `FatSecret__OAuth2__ClientId` |
| OAuth 2.0 client secret (optional) | `FatSecret__OAuth2__ClientSecret` |

The access token/secret come from a one-time authorization step, next.

### One-time OAuth 1.0a authorization

This grants the server access to your own FatSecret account. It's interactive - you approve
access in a browser - but doesn't need a callback server, since FatSecret shows a PIN you copy
back in. Do this once, from a terminal, with the consumer key/secret set:

```bash
export FatSecret__OAuth1__ConsumerKey="<your consumer key>"
export FatSecret__OAuth1__ConsumerSecret="<your consumer secret>"
fatsecret-mcp auth request
```

This prints a `token`, `token_secret`, and an `authorize_url`. Open the URL, log in, approve the
app, and copy the PIN it shows you. Then:

```bash
fatsecret-mcp auth exchange <token> <token_secret> <pin>
```

This prints an `access_token` and `access_token_secret` - it doesn't expire on its own. Add both,
plus the consumer key/secret, as environment variables wherever you run `fatsecret-mcp` from -
your shell profile for standalone use, or your MCP client's config for the `env` block shown
below.

## Run it

The server supports two transports, chosen at startup - **stdio by default**, or HTTP via a flag:

```bash
fatsecret-mcp                                       # stdio - for MCP clients that spawn the process directly
fatsecret-mcp --http --urls http://localhost:5102    # HTTP - a long-running server on a port
```

In HTTP mode the MCP endpoint is at `<url>/mcp` (Streamable HTTP), e.g. `http://localhost:5102/mcp`.

## Configure Claude Code, Claude Desktop, ChatGPT, or LM Studio

Do the credentials + one-time OAuth1 steps above first - that part is always interactive and
can't happen from inside a client's spawned process.

**Can these clients start `fatsecret-mcp` automatically?** Claude Code, Claude Desktop, and LM
Studio: yes - all spawn a local stdio process directly from their own config, no server to keep
running yourself. **ChatGPT: no** - it only connects to a reachable HTTPS URL, not a local
command. See its section below.

### Find the installed binary's absolute path

GUI-launched apps often don't inherit the PATH your terminal has, so the bare `fatsecret-mcp`
command name may not resolve even though it works in a shell. Use the absolute path instead:

- macOS/Linux: `~/.dotnet/tools/fatsecret-mcp`
- Windows: `%USERPROFILE%\.dotnet\tools\fatsecret-mcp.exe`

### Claude Code

```bash
claude mcp add fatsecret-mcp -- ~/.dotnet/tools/fatsecret-mcp
```

Restart or reconnect your Claude Code session - new MCP registrations aren't picked up
mid-session.

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

## Building from source / contributing

Not needed just to use the tool - see [`docs/DEVELOPER.md`](https://github.com/mregen/fatsecret-mcp/blob/main/docs/DEVELOPER.md)
in the repo for running from a clone, architecture notes, and how NuGet publishing works.

## License

MIT — see [LICENSE](https://github.com/mregen/fatsecret-mcp/blob/main/LICENSE).
