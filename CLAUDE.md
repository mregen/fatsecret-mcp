# CLAUDE.md

This file provides guidance to Claude Code when working in this repository.

## Project

A .NET-based Model Context Protocol (MCP) server for the [FatSecret Platform API](https://platform.fatsecret.com/docs/guides), hostable as a Docker container or dotnet tool. 
The developer has used FatSecret for calorie/nutrition tracking for 10+ years and wants LLM to be able to query and update their FatSecret data directly.
In the end, LLM should be able to sync data between various Fitness trackers and calory tracking services.

## Status as of 2026-08-19

- Pushed to GitHub: [mregen/fatsecret-mcp](https://github.com/mregen/fatsecret-mcp), public, branch `main`. MIT licensed (`LICENSE` + SPDX headers per source file).
- 14 issues filed across 3 milestones. **Milestone 1** (Prototype: MCP talking to an LLM) has #1/#2/#6 done, #3/#4/#5 open. **Milestone 2** (Full FatSecret feature coverage) is **fully closed** — #6 through #10 all done. **Milestone 3** (Harden & containerize) is open: #11 (token persistence), #12 (Dockerfile), #13 (more test coverage) remain; #14 (more docs) is mostly done (see below) but still open — its acceptance criteria also wants a Docker section, which waits on #12.
- **Working end to end today, against the real API and this developer's real FatSecret account**: `FatSecretMcp.slnx` + `src/FatSecretMcp/` (net10.0), `ModelContextProtocol.AspNetCore` 2.2.0, stateless Streamable HTTP at `/mcp`. Registered with Claude Code as `fatsecret-local-dev` (`http://localhost:5102/mcp`) — used live in-session for real tasks (reviewing this year's weight trend, backfilling missing breakfast entries, logging dinner, computing an August calorie-intake-vs-burned report from live diary + exercise data).
- **Now also a published-shape .NET global tool**, not just source: `PackAsTool`, command name `fatsecret-mcp`, dual transport chosen at startup (`Host.CreateApplicationBuilder` + stdio by default, `WebApplication.CreateBuilder` + `--http` flag for Streamable HTTP — the MCP SDK doesn't support registering both on one builder, so `Program.cs` branches before building the host). Versioned automatically via Nerdbank.GitVersioning (`version.json`, git-height-based SemVer2, no manual `<Version>` bump). Packs both `.nupkg` and a SourceLink-enabled `.snupkg`.
- **NuGet publishing pipeline built but not yet triggered**: `.github/workflows/build.yml` packs on every push to `main` and uploads the artifact; actual `nuget.org` publish is a separate manual-only `workflow_dispatch` job (`publish` boolean input, default false) gated behind the build+test job succeeding, using NuGet's Trusted Publishing (OIDC via `NuGet/login@v1`) instead of a stored API key — the developer already configured the matching policy on nuget.org (Repository Owner `mregen`, Repository `fatsecret-mcp`, Workflow `build.yml`, Environment `nuget-publish`, verified against `gh api` numeric IDs). A successful publish also tags and creates a GitHub Release (`gh release create`, version threaded from the packed filename via a job `output`) with both packages attached. Still has a `TODO_YOUR_NUGET_ORG_USERNAME` placeholder in the `NuGet/login` step's `user:` input — a developer action, not yet filled in — so nothing has actually been published yet.
- **Docs restructured around the nuget.org package as the primary entry point**: `README.md` now documents only what a stranger who finds the package on nuget.org needs — `dotnet tool install`, environment-variable credentials (`FatSecret__OAuth1__ConsumerKey` etc.), the `auth request`/`auth exchange` CLI, and Claude Code/Claude Desktop/ChatGPT/LM Studio configs — with no repo clone assumed, and internal doc links using absolute GitHub URLs so they still resolve when nuget.org renders the packed README. Everything that needs a clone (architecture notes, `dotnet user-secrets`-based local dev flow, publishing/Trusted Publishing setup, packing-and-installing a local build) moved to `docs/DEVELOPER.md`. This restructuring also caught and fixed a real bug: `AddUserSecrets<Program>()` was being called after builder construction in a way that put it *above* environment variables in config precedence — the opposite of normal ASP.NET Core behavior — silently ignoring env var overrides from an MCP client's `env` config; fixed by re-adding `AddEnvironmentVariables()` after `AddUserSecrets()` in all three `Program.cs` paths.
- Explored (informational only, explicitly not started) what running this as a multi-tenant cloud service would take — findings, ToS/tier constraints, and a 13-item work breakdown live in `docs/multi-tenant-cloud-service.md`. Conclusion: OAuth 1.0a 3-legged is the only viable per-user auth mechanism FatSecret offers (OAuth2 is app-level client-credentials only); current single-user `dotnet user-secrets`/env-var hosting stays as-is for now.
- **Auth — two flows, one fully working, one built but unverified**:
  - **OAuth 1.0a 3-legged** (`src/FatSecretMcp/Auth/`, issue #6): fully working. A ~10-year-old legacy FatSecret consumer key/secret turned out to have full `premier` scope, confirmed via real diary/weight/exercise data. Consumer key/secret and the completed access token/secret are stored in local `dotnet user-secrets` (`FatSecret:OAuth1:*`) — never commit them to the repo.
  - **OAuth 2.0 client-credentials** (`FatSecretOAuth2Client`, progresses issue #4): implemented (per-scope token caching) but **unverified** — needs a real client id/secret from issue #3, still open, still a manual user action (register a new FatSecret app). Confirmed live that barcode lookup and autocomplete specifically require this flow: the classic OAuth1-signed `server.api` dispatch returns "Unknown method" for them regardless of naming/URL variant tried, so they can't be reached via the working OAuth1 key the way diary/weight/exercise/search can.
  - **Docs bug found**: FatSecret's current three-legged guide says the request_token step uses POST; it actually requires GET. Worked around, not reported upstream. Locked in as a regression test (`FatSecretOAuth1ClientTests.GetRequestTokenAsync_SendsGetNotPost`).
- **Tool coverage** (`src/FatSecretMcp/Tools/`): `EchoTool` (placeholder), `FoodDiaryTools` (get/add/edit/delete, closes #7), `WeightTools` (get/add, closes #8), `ExerciseTools` (closes #9 — see note below), `FoodLookupTools` (barcode/autocomplete, closes #10, untested pending #3/#4). Food/recipe search (issue #5) is not implemented yet — also blocked on #3/#4.
- **Exercise API doesn't work like the others**: no independent add/edit/delete. FatSecret models a whole day as a fixed 1440-minute allocation across activities; every change is a "shift" of minutes between two activities (`exercise_entry.edit`). `ExerciseTools.ShiftExerciseTime` matches that directly rather than faking a CRUD shape. This developer's account has a connected fitness tracker ("Fitbit", exercise_id 179) that reserves the full day and can't be shifted from — confirmed live via a real "reserved" error, which also proves the request/signing path is correct.
- **Dependency notes**: switched to NuGet Central Package Management (`Directory.Packages.props`) ahead of adding the test project. Cherry-picked a few `.editorconfig`/`.gitattributes`/`NuGet.Config`/`Directory.Build.props` defaults from a sibling .NET project, dropping what's specific to that project being a multi-targeted library (target matrix, strong-name signing, packaging metadata) — see git history for exact diffs, not repeated here since Foundation is a separate, unrelated project this file shouldn't need to describe further. Also dogfoods `CryptoHives.Foundation.Security.Cryptography`'s `HmacSha1` in place of the BCL `HMACSHA1` in the OAuth1 signer, at the developer's request, purely to exercise that library in a real consumer — re-verified live afterward that signatures are still byte-correct.
- **Tests**: `tests/FatSecretMcp.Tests` (NUnit), 24 tests covering `OAuth1Signer` (against the canonical OAuth Core 1.0 vector) and `FatSecretOAuth1Client` (via a stub `HttpMessageHandler`, no network). `.github/workflows/build.yml` runs restore/build/test on push and PR to `main`.
- Next action is on the developer: register a new FatSecret app for OAuth 2.0 credentials (issue #3), which unblocks #4 and #5.

## Prior art (researched, not adopted)

Community FatSecret MCP servers already exist (TypeScript, Node-based):
- [fcoury/fatsecret-mcp](https://github.com/fcoury/fatsecret-mcp) — closest to complete: full OAuth 1.0a 3-legged flow with CLI helper, but only covers `search_foods`/`get_food`/`search_recipes`/`get_recipe`/`get_user_food_entries`/`add_food_entry`/`get_user_profile`. Single 29KB `src/index.ts`, 3 commits, 8 stars, placeholder `"author": "Your Name"` in package.json — quality signal is "weekend project," not hardened. Missing: weight tracking, exercise diary, saved meals, barcode lookup, autocomplete, image/NLP food recognition, diary entry edit/delete.
- [fliptheweb/fatsecret-mcp](https://github.com/fliptheweb/fatsecret-mcp) — similar scope, public search works without auth, diary ops need OAuth.

Conclusion: worth a look for reference, not worth forking. Building fresh in .NET.

## Architecture plan

- **MCP transport**: official [ModelContextProtocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) (`ModelContextProtocol.AspNetCore`), `.WithHttpTransport()` + `MapMcp()`, Streamable HTTP (stateless mode by default in SDK v2.0). Tool schemas auto-generated from C# method attributes — this removes nearly all JSON-RPC/protocol boilerplate the TS servers hand-rolled.
- **Auth — two flows, both needed**:
  - **OAuth 2.0 client-credentials** for the `basic`/`barcode` scopes: food/recipe search, autocomplete, barcode lookup. FatSecret **requires OAuth 2.0 tokens be requested through a proxy server**, not directly from a device — a self-hosted Docker MCP server naturally satisfies this requirement (it isn't extra work, it's the intended shape). See [OAuth 2.0 guide](https://platform.fatsecret.com/docs/guides/authentication/oauth2). *(Implemented as `FatSecretOAuth2Client`; unverified pending issue #3.)*
  - **OAuth 1.0a 3-legged** for the `premier` scope: user food diary, weight, exercise entries. No .NET built-in support; needs ~150 lines of HMAC-SHA1 signing (well-precedented pattern). See [OAuth 1.0a 3-legged guide](https://platform.fatsecret.com/docs/guides/authentication/oauth1/three-legged). *(Done — working end to end against the real API.)*
  - Other scopes available if wanted later: `localization`, `nlp`, `image-recognition`, `feedback`.
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

See GitHub Issues on [mregen/fatsecret-mcp](https://github.com/mregen/fatsecret-mcp/issues) for the full, current task list. Open issues:

- **#3** — register a new FatSecret developer app for OAuth 2.0 client-credentials. Developer action; blocks #4 (verifying `FatSecretOAuth2Client` against the real API) and #5 (food/recipe search tools, not started).
- Fill in the real nuget.org username (`TODO_YOUR_NUGET_ORG_USERNAME` in `build.yml`'s `NuGet/login` step) and trigger the first manual publish, once ready — developer action, no open issue tracks this specifically.
- **#11/#12** — token persistence design + Dockerfile, for a containerized host.
- **#13** — more test coverage (tool handlers, OAuth2 once it's verified).
- **#14** — README/DEVELOPER.md split is done; still open pending a Docker usage section once #12 lands.
