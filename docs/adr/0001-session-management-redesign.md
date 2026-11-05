# 0001 — Session management redesign (refresh restore, server logout, deviceGuid)

Status: Proposed — deferred, after MVP (2026-11-05) · Design by `architect`; scope deferred by user as too large for the current MVP stage

## Context

Three gaps were found while building the FE login slice (1.8, `docs/features/f01-login.md`):

1. `PUT /auth/refresh` requires the caller's own (expired) access token in the body and 400s before ever reading the httpOnly `refreshToken` cookie if that field is empty. Since the access token lives in memory only, a real page reload has already lost it, so reload always forces a fresh login even with a valid refresh cookie.
2. There is no server-side logout — `AuthController` has only `login`, `refresh`, `check`. A `UserTokens` row stays valid until its `RefreshTokenExpiryTime` with no way to revoke it early.
3. `deviceGuid` is a client-chosen GUID used to key `UserTokens` rows, raising the question of whether it still earns its place once the rest of the model changes.

Reading `AuthService.cs`/`AuthController.cs`/`UserToken.cs` while designing this also surfaced three pre-existing defects, independent of which option was chosen:

- Login looks up a `UserTokens` row by `DeviceGuid` alone (not `DeviceGuid + UserId`, unlike refresh since slice 0.4), then reassigns `UserId` on an already-tracked entity whose `UserId` is part of its primary key — EF Core throws when a tracked entity's key changes, so logging in as a second user on the same browser 500s today (`AuthService.cs:111,125-128`).
- Refresh rotates the stored token with no concurrency token, so two tabs refreshing at once can race: the loser silently fails or, in a true tie, both can write and desync the DB from the browser's cookie.
- A malformed/garbled refresh cookie throws an unhandled `ArgumentException`/`SecurityTokenException` inside `AuthService.RefreshToken`, surfacing as a 500 instead of a clean 400.

## Decision

Adopt **session model Option B**: one `UserTokens` row per login (not per device), identified by a server-generated session id embedded in the refresh token; `deviceGuid` is dropped from the client contract.

- **Session key**: `(UserId, LoginProvider, sid)` where `sid` is a new server-generated GUID placed in the refresh JWT. Login always inserts a new row (never reuses one) and, in the same commit, deletes the calling user's already-expired rows — self-cleaning, no scheduled job needed.
- **Refresh token shape**: slimmed to `nameid + sid + jti`; it no longer carries permission claims (those are rebuilt on every refresh from `RolePolicy` data, as today), keeping the cookie well under the ~4 KB browser limit as more permissions are added.
- **Storage**: the refresh token is hashed (SHA-256, lowercase hex, unsalted — appropriate for a high-entropy token, not a password) before being stored in `UserTokens.RefreshTokenHash`; the raw `RefreshToken` column is stopped being written.
- **Reload restore (gap 1)**: `RefreshTokenReq.Token` becomes optional. When a client sends it, today's rule is unchanged — a still-valid access token refuses refresh, only an expired one may refresh. When it is absent (the reload case, no in-memory token), the server identifies the user and session from the verified refresh token/cookie alone and skips the old-token-expiry check, since there is no old token to check.
- **Server logout (gap 2)**: new `POST /auth/logout`, `[AllowAnonymous]` (the access token may already be expired; the cookie is the credential). Deletes the `UserTokens` row by `sid` read from the refresh cookie (a validly signed `sid` proves the session was genuinely issued, so this works even if the stored hash has since rotated away from what the caller holds) and always clears the cookie. Always returns `200 success:true`, including for a missing/invalid/unknown cookie — idempotent, reveals nothing to a prober.
- **deviceGuid (gap 3)**: dropped from `LoginReq`/`RefreshTokenReq` and from the FE entirely (`FE/src/api/deviceGuid.ts` deleted). It never did security work (client-chosen, non-secret, stored in `localStorage` by design) — removing it only removes the defects it caused (the `Guid.Empty` collision case, the login-user-switch 500).
- **Multi-tab refresh race (defect above, formalized as part of this decision, not deferred)**: a concurrency token on `RefreshTokenHash` (`IsConcurrencyToken()`) makes a losing rotation attempt fail with a distinguishable conflict rather than silently corrupting state or silently logging a tab out. The server returns a distinct error (not the generic "Invalid tokens") when the race is lost, so the client can tell the two cases apart. The FE wraps `refreshAccessToken` in a `navigator.locks` (Web Locks API — a browser platform API, no new dependency) cross-tab lock so concurrent tabs serialize their refresh instead of racing in the first place; a browser without `navigator.locks` falls back to today's in-tab single-flight only, and on the server-reported race-loss code the client retries the refresh exactly once (reading the now-current cookie) rather than failing the user's action outright.
- **Malformed input (pre-existing defect 3 above)**: every parse/validation failure in the cookie-only refresh path returns `400` with a new error code, never an unhandled exception.

The destructive cleanup — dropping the now-dead `DeviceGuid` and `RefreshToken` columns — is **deferred**, not part of this decision's immediate scope. `DeviceGuid` is made nullable now (rows written under the old model keep their value; new rows leave it null) so the column drop can happen later as its own reviewed, backed-up release.

## Consequences

- Cookies issued before this ships stop working once (old shape, no `sid`) — every user logs in again after deploy. One-off, not a recurring cost.
- `BE/TaskFlow.Tests/Http/RefreshTokenTests.cs`, which queries by `DeviceGuid`, needs rewriting against the new key.
- `docs/features/f01-login.md`'s R2, R5, AC6, AC7, AC11 and its Q1/Q3 open questions are superseded and need rewriting once slice 2.3 lands (tracked in `docs/ROADMAP.md`).
- FE slice 1.8's single-flight/one-shot-retry logic in `client.ts` is unchanged in shape; only the refresh request body and the "skip refresh with no token" guard in `RequireAuth.tsx` change (from a hard skip to an actual restore attempt).
- Per-session revocation becomes possible (a prerequisite this decision enables but does not itself build: "log out everywhere" / listing active sessions).
- A stolen refresh cookie's blast radius is now bounded by "until logout" instead of only "until natural expiry" — a real improvement — but an already-issued access token (≤15 min) still works after logout, since the API does no per-request database check (accepted: stateless JWT, unchanged by this decision).
- No new CSRF mitigation (double-submit token, custom header) is added now. `SameSite=Strict` plus the existing CORS allow-list is judged sufficient while both affected endpoints keep a required JSON body (even if every field is optional) and the deployment target is same-site. This is explicitly revisited at `/ship setup` once the deployment target (`CLAUDE.md` Deployment: TBD) is known — a different-site or sibling-subdomain deployment would change this judgment.
- No new table, no new architectural pattern: `UserTokens` (the existing ASP.NET Identity-derived entity) is reused, nullable-ized rather than replaced.

## Alternatives considered

- **Option A — keep one row per device, key it so the cookie alone can address it.** Smallest schema change (one new column), keeps nearly all existing `AuthService` logic. Rejected as the primary path because the client still picks the key (`deviceGuid`), which is exactly what caused the login-500 defect and the `Guid.Empty`/orphan-row problems; it also doesn't support more than one concurrent session per browser profile per user.
- **Option C — new `UserSessions` table, opaque random-token cookie instead of a refresh JWT.** No signing secret needed for refresh, smallest possible cookie, database is the sole source of truth. Rejected for now: the largest change (new entity, new EF configuration, a pattern not currently used in this codebase), and Option B's slimmed refresh token already resolves the cookie-size concern that was C's main advantage. May be revisited if the refresh-JWT approach proves awkward in practice.
- **Reuse detection** (revoking a session outright if a rotated-out-but-correctly-signed refresh token is ever presented again) was considered and **not** adopted now — it conflicts with legitimate multi-tab races unless the race-handling above is bulletproof first; left as a future hardening step.
- **Removing the "only refresh an actually-expired access token" rule entirely** (since a client can now refresh with no token at all) was considered and rejected — the rule still has value as a guard against client bugs even though it no longer stops an attacker holding a stolen cookie; kept as "checked only when a token is present."
