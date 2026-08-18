# fatsecret-mcp

A .NET-based Model Context Protocol (MCP) server for the [FatSecret Platform API](https://platform.fatsecret.com/docs/guides), hostable as a Docker container.

## Status

Early scaffolding. Not yet functional.

## Plan

- Built on the official [ModelContextProtocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) (`ModelContextProtocol.AspNetCore`), using Streamable HTTP transport (`MapMcp`).
- Auth:
  - OAuth 2.0 client-credentials flow for the `basic` scope (food/recipe search, autocomplete, barcode lookup).
  - OAuth 1.0a 3-legged flow for the `premier` scope (user food diary, weight, exercise entries) — FatSecret requires OAuth 2.0 tokens be requested through a proxy server, which a self-hosted Docker container naturally satisfies.
- Target: `net10.0`, multi-stage Dockerfile, non-root container user.

## License

TBD
