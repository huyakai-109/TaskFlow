# 0004 — SignalR for presence

- **Context:** The board needs a live "who's viewing" list ([f07-presence.md](../features/f07-presence.md)). The client keeps its access token in memory only and polls the bell every 30 s. There is no push channel today. One API instance; deployment target TBD; Redis is already in the stack.
- **Decision:** ASP.NET Core SignalR (shared framework, no server NuGet) with one hub `/hubs/presence` and an in-memory `IPresenceTracker`. JWT via `access_token` query string, limited to `/hubs/*`, with `CloseOnAuthenticationExpiration`. Client dependency `@microsoft/signalr` (MIT, ~40–50 KB gzipped).
- **Approved 2026-11-13, conditional on two binding requirements** (both are acceptance criteria for the R2-7 slice, not aspirations):
  1. **No query string is logged, by the API or by any reverse proxy, for any `/hubs/*` path.** On the API side: `Serilog:MinimumLevel:Override` pins `Microsoft.AspNetCore.Hosting.Diagnostics` and `Microsoft.AspNetCore.Http.Connections` to `Warning` explicitly (not left to the current incidental `Microsoft: Error` override), and a Serilog enricher redacts the `access_token` value from any log event whose path starts with `/hubs/`. On the proxy side: whatever reverse proxy is chosen at `/ship setup` must be configured not to log query strings for `/hubs/*` — recorded here so it is checked at that time, not forgotten.
  2. **`access_token` is accepted via query string only on hub endpoints.** `JwtBearerEvents.OnMessageReceived` must check `context.HttpContext.Request.Path.StartsWithSegments("/hubs")` before reading the query token; every ordinary HTTP route continues to require the `Authorization: Bearer` header only. A test asserts that a request to a non-hub route with a bearer token in the query string is rejected as unauthenticated.
- **Consequences:**
  - Long-lived connections: the reverse proxy must support WebSockets and must not log query strings.
  - Log levels must be pinned and an access-token redaction enricher added.
  - Multi-instance later needs a Redis backplane NuGet, a Redis-backed tracker, and sticky sessions or a WebSockets-only transport.
  - New metrics meter (`Microsoft.AspNetCore.Http.Connections`). Hub integration tests need `Microsoft.AspNetCore.SignalR.Client` in the test project (test-only dependency).
- **Alternatives rejected:**
  - Polling a presence endpoint: still needs server-side heartbeat state, adds seconds of lag, multiplies requests.
  - Raw WebSockets: reimplements reconnect, groups and keepalive that SignalR already provides.
  - Server-Sent Events: one direction only, so join/leave would need separate HTTP calls; same token-in-URL problem.
  - A third-party hosted realtime service: adds an outbound dependency and ongoing cost for no local benefit.