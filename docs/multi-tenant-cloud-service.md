# Multi-tenant cloud service: work breakdown

Research and sketch for turning `fatsecret-mcp` from a single-user local tool into a cloud
service: one developer's own FatSecret API registration, serving many end users who each see
only their own data. Not started — captured here for later reference. Current hosting stays
single-user, local `dotnet user-secrets`, unchanged.

## OAuth1 vs OAuth2

**OAuth 1.0a 3-legged is the only option** — FatSecret's OAuth 2.0 is client-credentials only
(app-level, no per-user delegation). Their three-legged guide describes exactly this pattern:
one app, many end users, each completing their own 3-legged grant. `FatSecretOAuth1Client`
(`src/FatSecretMcp/Auth/FatSecretOAuth1Client.cs`) is already fully parameterized per access
token, so the signing/calling code carries over unchanged — what's missing is everything
*around* it: per-user token storage, a real OAuth callback (not the oob/PIN flow), and a way
for the service to know which end user is calling in the first place.

## FatSecret Terms of Use / tier findings

Confirmed via FatSecret's Terms of Use and API editions page — not a gray area:

- Explicitly sanctioned: "Full 3-legged OAuth... allows you... to provide your application to
  users who are members of FatSecret.com."
- The real constraint is capacity, not permission. The free **Basic** tier caps at **5,000 API
  calls/day shared across all end users of the app** (not per-user) and is US-only data.
- **Premier** (needed for real volume or non-US data) is "pricing upon request" — no self-serve
  upgrade, contact FatSecret directly.
- Attribution ("powered by FatSecret" + link to their Terms) is required on Basic/Premier-Free,
  waived on paid Premier.

## MCP SDK authorization gap

`ModelContextProtocol.AspNetCore` only ships *resource-server* building blocks —
`AddAuthentication().AddJwtBearer(...).AddMcp(...)` (serves RFC 9728 protected-resource
metadata) and `app.MapMcp().RequireAuthorization()`. There's no token-issuing authorization
server in the SDK — no Dynamic Client Registration (DCR), no PKCE verification, no consent UI.
Since public MCP clients need DCR (RFC 7591), hand-building a compliant OAuth 2.1 AS is
comparable in scope to standing up Duende IdentityServer from scratch. Evaluating/adopting an
existing AS is its own item below, separate from the resource-server wiring, since the two have
very different risk/effort profiles.

## Decision already made

End-user identity at the MCP layer should use the **official MCP Authorization spec
(OAuth 2.1)**, not a custom API-key scheme — so standard MCP clients (e.g. Claude.ai's remote
connector) can authenticate normally.

## Work breakdown

13 items, roughly in dependency order:

1. **[Business] Confirm FatSecret usage tier and rate-limit runway.** Document the Basic-tier
   5,000 calls/day *shared* ceiling and US-only data; decide to launch on Basic for early users
   and revisit before hitting the cap, or contact FatSecret about Premier upfront if scale is
   the point from day one. User action, not code. No dependencies — do this first.

2. **[Spike] Decide target hosting platform.** Token storage (#5), the OAuth2.1 AS choice (#3),
   and the OAuth1 callback URL (#6) all silently depend on where this runs and what domain it
   has. Doesn't need full deployment automation yet (that's #11) — just an early decision so
   later items aren't blocked on an implicit choice.

3. **Select or stand up an OAuth 2.1 authorization server with DCR + PKCE.** The load-bearing
   finding above: the SDK doesn't provide this. Evaluate self-hosted Duende IdentityServer vs.
   a hosted IdP with existing MCP-oriented guides (WorkOS AuthKit, Auth0) rather than
   hand-rolling DCR/PKCE/token-issuance/consent. This establishes the stable per-tenant identity
   (subject/claim) that #5 keys its token store by. Depends on #2.

4. **Wire ASP.NET Core resource-server validation for MCP Authorization.**
   `AddAuthentication().AddJwtBearer(...).AddMcp(options => options.ResourceMetadata = ...)` +
   `app.MapMcp().RequireAuthorization()`, validating tokens issued by #3, serving
   `/.well-known/oauth-protected-resource`. Changes `src/FatSecretMcp/Program.cs`'s HTTP path.
   Depends on #3.

5. **Design & implement per-user FatSecret token store.** Replaces the single
   `dotnet user-secrets` value with real persistent storage keyed by the subject established in
   #3 — schema roughly `Users(SubjectId, FatSecretAccessToken, FatSecretAccessTokenSecret,
   ConnectedAt)`. Use ASP.NET Core's `IDataProtector` for encryption at rest (framework-native,
   identical locally and in any container) — configure the key-protection backend
   (`IXmlRepository`/`ProtectKeysWith...`) as pluggable config from the start, so swapping to
   cloud KMS/blob storage once #2 lands is a config change, not a rewrite. Depends on #3 (needs
   the subject to key by).

6. **OAuth1 callback endpoint + pending-authorization flow.** `AuthCli`'s oob/PIN flow
   (`src/FatSecretMcp/AuthCli.cs`) is fine for one developer's one-time setup; hosted multi-user
   use needs a real redirect callback (e.g. `GET /connect/fatsecret/callback`) using
   `FatSecretOAuth1Client.GetAccessTokenAsync` unchanged, plus a short-lived
   pending-request-token store keyed by state. Soft dependency on #2 (needs a real HTTPS
   domain), not full deployment.

7. **Refactor tool/auth plumbing to per-request-user scoping.** `FatSecretPremierApi` and
   `AddFatSecretClients` (`src/FatSecretMcp/Auth/FatSecretPremierApi.cs`,
   `ServiceCollectionExtensions.cs`) currently resolve one static config token as a singleton.
   Needs to become scoped, resolving `HttpContext.User` (from #4) → looking up that user's
   token (from #5) per request. Also decide the MCP HTTP transport's `SessionMode` here
   (Stateless recommended for horizontal scaling) rather than leaving it implicit. Depends on
   #4 and #5.

8. **User onboarding / "connect your FatSecret account" flow, including disconnect.** Ties #6
   and #3/#4 together: authenticate to the service, then prompt to connect FatSecret if not
   already connected. Include token revocation ("disconnect," deletes the stored token) here.
   Depends on #6, #7.

9. **Rate limiting / quota handling across the shared FatSecret app key.** Per #1's finding,
   throttle/queue per-user against the app-wide daily cap and surface clear errors through MCP
   tool calls instead of raw FatSecret errors. Depends on #7.

10. **End-to-end interop test against a real MCP client completing the OAuth 2.1 + DCR flow.**
    E.g. Claude.ai's remote connector. Third-party client behavior is an external risk worth its
    own explicit verification rather than assuming it "should just work" once the pieces exist.
    Depends on #8.

11. **Containerize + deploy to real cloud hosting with a production datastore.**
    Supersedes/builds on the existing issue #12 (Milestone 3, not yet started). Depends on #2,
    #5.

12. **Cross-cutting hardening pass.** HTTPS/HSTS enforcement, dependency/secret scanning, the
    app's own consumer-key rotation policy, and multi-tenant logging/observability isolation
    (logs/telemetry must not leak across users' data). Deliberately narrow —
    encryption-at-rest lives in #5, revocation in #8, not duplicated here.

13. **Privacy Policy + Terms of Service + compliance review.** Required once handling other
    people's health/diet data, not just the developer's own. Flag explicitly as needing real
    legal-caliber review, not an engineering checkbox — do before onboarding real users,
    independent of the rest.
