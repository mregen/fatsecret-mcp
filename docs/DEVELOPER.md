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

This isn't primarily a security gate (the package contains no credentials either way - see the
README's [Security](../README.md#security) section for the actual concern) so much as a "not
polished enough yet" one: OAuth 2.0 is still unverified and food/recipe search isn't
implemented. Trigger a manual publish run once that's no longer true.
