# F07 — Presence ("who's viewing")
Status: **Signed off 2026-11-13** (scope confirmed as-drafted: a `board` room and a per-`task:{id}` room, each showing only userName + avatar, no view-history persisted) · ADRs [0004](../adr/0004-signalr-for-presence.md) (SignalR, conditional sign-off — two binding log/auth requirements) and [0005](../adr/0005-real-time-push-scope-change.md) (scope change, signed) · Depends on F08 for displayName/avatar (ships with `userName` first)

## Scope
v1: live viewer list per scope — `board` and `task:{id}`. Replaces the board toolbar AvatarStack + TeamCapacityPopover (COMING-SOON A9). Out of v1: board/notification push (roadmap below), typing/edit locks, "last seen", cross-page online status.

## Hub contract
Endpoint `/hubs/presence` (SignalR, JSON protocol), `[Authorize(Policy = ViewTasks)]`.

Client → server:
- `Join(scope: string)` → `Viewer[]` snapshot. Scope grammar: `board` | `task:{long ≥1}`; anything else → `HubException("Presence.Join.InvalidScope")`. Max 4 scopes per connection → `Presence.Join.TooManyScopes`. Task existence is not checked (no data leak: every task is visible to every `ViewTasks` holder).
- `Leave(scope: string)` → void, idempotent.

Server → client:
- `PresenceChanged({ scope, viewers: Viewer[] })` to the scope group on every join/leave/disconnect that changes the user set (full list, not deltas — list ≤ team size).

`Viewer = { userId, userName, displayName | null, avatarUrl | null }`, unique per user (tabs deduped), ordered by join time.

## Server design
- `IPresenceTracker` singleton (in-memory): `scope → userId → set<connectionId>`, locked per scope. `OnDisconnectedAsync` removes the connection from every scope it joined. A user leaves a scope when their last connection there leaves.
- Keepalive 15 s, `ClientTimeoutInterval` 30 s (defaults): a killed tab disappears within ~30 s.
- `MaximumReceiveMessageSize` 4 KB. Join/Leave beyond 5/s per connection ignored.
- Lives in `TaskFlow.Api` (hub + tracker); no `BusinessLogic` dependency in v1.

## Authentication
Browsers cannot set headers on a WebSocket upgrade; the token travels as `?access_token=`:
- `JwtBearerEvents.OnMessageReceived` reads the query token **only** when the path starts with `/hubs/`.
- `HttpConnectionDispatcherOptions.CloseOnAuthenticationExpiration = true`: the connection closes when the 15-min access token expires; the client's `accessTokenFactory` runs the existing single-flight refresh and reconnects. Deactivated users or role changes therefore take effect within ≤15 min.
- **Risk:** query strings are logged by servers and proxies. **Mitigation:** pin `Serilog:MinimumLevel:Override` for `Microsoft.AspNetCore.Hosting.Diagnostics` and `Microsoft.AspNetCore.Http.Connections` at `Warning` explicitly (today only the broad `Microsoft: Error` override protects it), add an enricher that redacts `access_token` from any logged path/query, and require any reverse proxy (decided at `/ship setup`) not to log query strings for `/hubs/*`. Hardening option (open decision 17): a one-time 30 s ticket from `POST /api/v1/realtime/ticket` stored in Redis instead of the JWT.
- CORS: the existing allow-list policy with credentials covers `/hubs/presence/negotiate`.

## Client
`@microsoft/signalr` (**new dependency**, MIT, ~40–50 KB gzipped). One connection per tab, created in `app/` after login, `withAutomaticReconnect`; on `onreconnected` re-`Join` every active scope (new connectionId, server state gone). The board page joins `board`; task detail joins `task:{id}`; unmount → `Leave`. A hidden tab stays joined (Confluence-style behaviour). Presence stack: up to 5 avatars + "+N", tooltip names, initials fallback; never blocks the page (connection failure → stack hidden, no error banner).

## Scale and operations
v1 is single-instance. Multi-instance needs both a backplane (`Microsoft.AspNetCore.SignalR.StackExchangeRedis`, new NuGet) and a Redis-backed tracker (per-connection keys with TTL refreshed by heartbeat) — the backplane alone does not share presence state — plus sticky sessions or a WebSockets-only transport. Metrics: add the `Microsoft.AspNetCore.Http.Connections` meter to OpenTelemetry. The reverse proxy must allow WebSocket upgrade and an idle timeout ≥ 60 s.

## Privacy
Only "who is on this page now"; nothing persisted, join/leave logged at Debug only, no opt-out in v1 (open decision 20).

## Roadmap (not v1)
v2: `BoardChanged { taskId, version }` to group `board` after commit → client invalidates `['board']` (replaces refetch-on-focus); `NotificationCreated` to a user → bell refetch (replaces the 30 s poll it has today); Activity "N new". Requires an `IRealtimePublisher` port in `BusinessLogic` implemented in `Api`, called **after** `SaveChanges()` succeeds, fire-and-forget (a push failure never fails the request).

## Acceptance criteria
AC1 two users on the board see each other within 2 s. AC2 the same user in two tabs appears once; closing one tab keeps them; closing both removes them within 30 s. AC3 an unauthenticated or token-less connect → 401, no connection. AC4 an invalid scope → error, no join. AC5 a fifth scope → refused. AC6 after the access token expires the connection closes and the client reconnects with a fresh token and re-joins. AC7 a dropped network reconnects and the viewer list is restored. AC8 no access token appears in any log line at the configured levels (ADR 0004's two binding conditions — pinned log levels/redaction enricher on the API, no query-string logging on the proxy). AC9 **replaces the ≤15 min+30s bound**: when a user is deactivated ([f08](f08-accounts-profile.md) "Quick revocation on deactivation"), their presence connections are proactively closed by the server within the deactivation request's own commit (the presence tracker, already indexed by userId, aborts every live connection for that user) — disappearance is near-immediate, not bounded by token expiry. AC10 a request to any non-hub route carrying `access_token` only in the query string (no `Authorization` header) is rejected as unauthenticated (ADR 0004 condition 2).