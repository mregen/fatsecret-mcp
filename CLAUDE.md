# CLAUDE.md

This file provides guidance to Claude Code when working in this repository.

## Project

A .NET-based Model Context Protocol (MCP) server for the [FatSecret Platform API](https://platform.fatsecret.com/docs/guides), hostable as a Docker container. The user has used FatSecret for calorie/nutrition tracking for 10+ years and wants Claude to be able to query and update their FatSecret data directly.

## Status as of 2026-08-18

- Pushed to GitHub: [mregen/fatsecret-mcp](https://github.com/mregen/fatsecret-mcp), public, branch `main`.
- 14 issues filed across 3 milestones (Prototype → Full feature coverage → Harden & containerize) covering the full build-out; see GitHub Issues for the authoritative task list.
- **Milestone 1 in progress**: issues #1 (scaffold) and #2 (MCP host + smoke test) are done — `FatSecretMcp.slnx` + `src/FatSecretMcp/` (net10.0), `ModelContextProtocol.AspNetCore` 2.2.0 wired up with stateless Streamable HTTP at `/mcp`, verified end to end with a placeholder `echo` tool via raw JSON-RPC.
- **Auth key finding**: a ~10-year-old FatSecret OAuth 1.0 consumer key/secret was confirmed still live (2026-08-18, signed 2-legged request against `foods.search` succeeded). Decision: keep the original two-flow plan below — this legacy key is earmarked for OAuth 1.0a 3-legged (`premier` scope, issue #6) only; a **new** app registration is still needed for OAuth 2.0 client-credentials (`basic` scope, issue #3). The legacy key/secret is already stored in local `dotnet user-secrets` (`FatSecret:OAuth1:ConsumerKey`/`ConsumerSecret`) — never commit it to the repo.
- Next action is on the user: register a new FatSecret app for OAuth 2.0 credentials (issue #3), which unblocks #4 and #5.

## Prior art (researched, not adopted)

Community FatSecret MCP servers already exist (TypeScript, Node-based):
- [fcoury/fatsecret-mcp](https://github.com/fcoury/fatsecret-mcp) — closest to complete: full OAuth 1.0a 3-legged flow with CLI helper, but only covers `search_foods`/`get_food`/`search_recipes`/`get_recipe`/`get_user_food_entries`/`add_food_entry`/`get_user_profile`. Single 29KB `src/index.ts`, 3 commits, 8 stars, placeholder `"author": "Your Name"` in package.json — quality signal is "weekend project," not hardened. Missing: weight tracking, exercise diary, saved meals, barcode lookup, autocomplete, image/NLP food recognition, diary entry edit/delete.
- [fliptheweb/fatsecret-mcp](https://github.com/fliptheweb/fatsecret-mcp) — similar scope, public search works without auth, diary ops need OAuth.

Conclusion: worth a look for reference, not worth forking. Building fresh in .NET.

## Architecture plan

- **MCP transport**: official [ModelContextProtocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) (`ModelContextProtocol.AspNetCore`), `.WithHttpTransport()` + `MapMcp()`, Streamable HTTP (stateless mode by default in SDK v2.0). Tool schemas auto-generated from C# method attributes — this removes nearly all JSON-RPC/protocol boilerplate the TS servers hand-rolled.
- **Auth — two flows, both needed**:
  - **OAuth 2.0 client-credentials** for the `basic` scope: food/recipe search, autocomplete, barcode lookup. FatSecret **requires OAuth 2.0 tokens be requested through a proxy server**, not directly from a device — a self-hosted Docker MCP server naturally satisfies this requirement (it isn't extra work, it's the intended shape). See [OAuth 2.0 guide](https://platform.fatsecret.com/docs/guides/authentication/oauth2).
  - **OAuth 1.0a 3-legged** for the `premier` scope: user food diary, weight, exercise entries. No .NET built-in support; needs ~150 lines of HMAC-SHA1 signing (well-precedented pattern). See [OAuth 1.0a 3-legged guide](https://platform.fatsecret.com/docs/guides/authentication/oauth1/three-legged).
  - Other scopes available if wanted later: `barcode`, `localization`, `nlp`, `image-recognition`, `feedback`.
- **Target framework**: `net10.0` (latest LTS-track SDK available; single-target, no multi-targeting matrix, strong-name signing, or crypto-specific constraints needed for this project).
- **Docker**: multi-stage Dockerfile (SDK image to build, aspnet/runtime image to run), non-root user, config via env vars / mounted secrets (not baked into the image).
- **Token persistence**: needs a real design decision for a containerized/stateless host — likely a mounted volume or external secret store, *not* the TS servers' pattern of a local dotfile in the user's home directory (`~/.fatsecret-mcp-config.json`), which doesn't translate to a container.

## Effort estimate (from prior discussion)

| Piece | Effort |
|---|---|
| Project scaffold + Dockerfile | 0.5 day |
| MCP host wiring + smoke test via MCP Inspector | 0.5–1 day |
| OAuth 2.0 client-credentials (`basic` scope) | 0.5 day |
| OAuth 1.0a 3-legged (`premier` scope) | 1–1.5 days |
| Endpoint coverage (search, get, diary CRUD, weight, exercise, barcode, autocomplete) | 2–3 days, scales with scope chosen |
| Token persistence design for containerized host | 0.5–1 day |
| Tests + README | 1–1.5 days |

- **MVP** (parity with the TS servers: auth + food/recipe search + basic diary add/get): ~3 days
- **Fuller build** (+ weight, exercise, barcode, autocomplete, proper secret handling): ~1.5 weeks

## Next steps (pick up here)

See GitHub Issues on [mregen/fatsecret-mcp](https://github.com/mregen/fatsecret-mcp/issues) for the full, current task list. Immediate next step: issue #3 — register a new FatSecret developer app for OAuth 2.0 client-credentials (user action, blocks #4 and #5). Containerization (Milestone 3) is deliberately deferred until Milestone 1 (prototype talking to an LLM) is done.
