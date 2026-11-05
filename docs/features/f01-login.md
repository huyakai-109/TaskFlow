# F01 — Login (client slice 1.8)

| | |
|---|---|
| **Source** | Gap found after slices 1.5–1.7: the board calls the API with no token, so every call gets a 401. No BRS section owns login directly — it is the primitive the F01 permission table (`docs/features/f01-workflow.md` → *Permissions*) needs on the client. |
| **Status** | Ready for Review — `review-change`: APPROVE WITH RECOMMENDATIONS on `c606862`, 2026-11-04 (no Must-fix; see commit for detail) |
| **Depends on** | `BE` slice 0.4 (httpOnly refresh cookie, `permissions: string[]` on login/refresh — done) |
| **Related** | `docs/features/f01-workflow.md` *Permissions* (the `Tasks` permission set this slice stores and reads) |

**Goal** — a user can sign in, the client holds the access token in memory and calls the API with it, and a route guard sends an unauthenticated visitor to login and back to where they started. The server's `permissions[]` are captured client-side for a later slice to gate UI with; none is gated yet.

This is a frontend-only slice. No backend file changes; two gaps in today's contract are raised as open questions below rather than worked around.

## Scope

In scope: login screen, wiring `refreshAccessToken`, a route guard with silent-refresh-then-redirect, a client-side permission store fed from `login`/`refresh` responses, logout.

Out of scope: user management (no endpoint exists), a "my account" screen, remember-me, password reset, the sidebar's user line (`docs/ui.md` — sidebar itself is not in the MVP), any permission-gated UI beyond what already exists (`ReopenTasks` is already correctly gated server-side through `allowedTransitions`; this slice stores `permissions[]` but adds no `useHasPermission`-style hook or new gated affordance — the next slice that needs one builds it against the stored data), silent session restore across a real reload and server-side logout (both accepted as MVP limitations, tracked as backend follow-ups in `docs/ROADMAP.md`, not fixed here).

## Rules

- **R1** The access token lives in memory only (`FE/src/api/client.ts`'s existing `setAccessToken`/`hasAccessToken`); never `localStorage`/`sessionStorage`/a cookie the client sets itself.
- **R2** `deviceGuid` is not a credential. It is generated once per browser installation and kept in `localStorage` so login and refresh address the same `UserToken` row across reloads (contrast with R1).
- **R3** A request that gets a `401` is retried after exactly one refresh attempt, never looped: refresh itself failing (or a second `401` after the retry) clears the session and routes to login.
- **R4** Permissions are data from the server (`login`/`refresh` response), never inferred from a role name on the client. A permission check is presentation only — every server endpoint still enforces its own policy.
- **R5** Logout is a client-only action today (see *Open questions* — no logout endpoint exists). It clears the in-memory token, the permission store, and the TanStack Query cache, then routes to `/login`.
- **R6** The originally requested path survives a login round trip: `/tasks/42` while unauthenticated → login → `/tasks/42`, not `/board`.
- **R7** Refresh is single-flight: at most one `PUT /auth/refresh` is in flight at a time. If several requests get a `401` concurrently (e.g. the board and notifications firing together), they all await the *same* in-flight refresh promise instead of each starting their own, then all retry once that promise settles. React 18/19 `StrictMode`'s dev-only double-invoke of effects must not cause a second refresh either — the dedupe lives in the module-level request layer (`client.ts`), not in a component effect.

## Acceptance criteria

1. **Successful login.** Valid `username`/`password` → `POST /auth/login` with a persisted `deviceGuid` and `credentials: 'include'` → access token stored in memory, `permissions` stored client-side, redirect to the originally requested path (or `/board` if none) with history replaced (no back-button bounce to the login screen).
2. **Invalid credentials.** Server returns `400` with `error: "Invalid credential"` → form shows "Incorrect username or password.", password field cleared, username kept, submit re-enabled, focus stays usable (no silent failure).
3. **Account will be locked out.** `error: "Your account will be locked out"` → shown as a distinct warning-toned message naming that one more failure locks the account. The request still failed: no token stored.
4. **Account is locked out.** `error: "Your account has been locked out"` → shown as a distinct message telling the user to contact an administrator (no self-service unlock exists).
5. **Network/transport failure** (`fetch` rejects — offline, DNS, connection refused). Shown as "Could not reach the server. Check your connection and try again."; the form is left filled in and resubmittable.
6. **Silent restore on reload, when it can work.** With a live refresh cookie and an in-memory token still present (no actual reload — e.g., an SPA navigation after a `401`), the one-shot refresh-then-retry in AC8 covers the "still logged in" case. A real reload always clears the in-memory token (R1); AC7 covers what happens then, which — see *Open questions* Q1 — is always a redirect to login under today's contract, not a silent restore.
7. **Reload with no in-memory token.** `RequireAuth` does **not** call `PUT /auth/refresh` when there is no in-memory access token at all (`hasAccessToken()` is false) — that call is known to fail (`400 "Invalid tokens"`, Q1), so it is skipped outright rather than sent and ignored. The guard treats "no token" the same as "no session": redirect to `/login` immediately, with `state.from` set to the path that was requested, no flash of board content first, no needless request. The skip is a single `if` in one place so it is trivial to remove once the backend accepts cookie-only refresh (Q1's follow-up).
8. **401 mid-session (token genuinely expired, cookie still valid).** A board/task call returns `401` → the client refreshes once using the in-memory (now-expired) token and the cookie → on success, the original request is retried transparently and the caller never sees the failure; the refreshed `permissions` replace the stored ones.
9. **401 mid-session, refresh also fails** (cookie expired, revoked, or a genuine second `401`). The client clears the session and the next render of `RequireAuth` redirects to `/login` with `state.from` set to the current path; no infinite retry loop (confirm by code inspection: the retry flag is single-shot, not re-entered).
10. **Concurrent 401s share one refresh (R7).** When two or more requests are in flight together (e.g. the board query and the notifications query) and both get a `401`, only one `PUT /auth/refresh` is sent; the other request(s) await that same promise and then retry with the new token. Confirmed by code inspection (one shared in-flight promise, reset after it settles) and, in the browser, by the Network tab showing exactly one `refresh` call when both board and notifications 401 together. `StrictMode`'s double-invoke in dev must not produce a second concurrent refresh.
11. **Logout.** Clicking "Log out" clears the in-memory token, the permission store and the TanStack Query cache (no stale board data flashes if a different user logs in next), and navigates to `/login`. No server call is made (R5, Q3 — accepted for this slice, tracked as a backend follow-up in `docs/ROADMAP.md`).
12. **Keyboard and screen reader.** Tab order: username → password → submit; `Enter` in either field submits; the error banner is a `role="alert"` live region; both inputs have visible associated `<label>`s; focus is never trapped or lost on error.
13. **Refresh cookie actually persists and is sent.** After a successful login, the browser's Application/Storage panel shows the `refreshToken` cookie (httpOnly, `Secure` off only in dev, `SameSite=Strict`, `path=/api/v1/auth`); the Network tab's request headers for the subsequent `PUT /auth/refresh` show that cookie being sent back. This is a verification step, not new client code (the cookie is entirely server-set) — it confirms slice 0.4's mechanism still holds end-to-end through this slice's UI.

## File list (not yet created — this is a plan)

| File | Purpose |
|---|---|
| `FE/src/api/deviceGuid.ts` | Get-or-create the per-installation `deviceGuid`, `localStorage`-backed (R2) — the one place that touches storage for this value |
| `FE/src/api/auth.ts` | `login(input)` → `POST /auth/login`; sets the access token on success, returns `{ permissions }`. Mirrors `tasks.ts`'s per-endpoint function style |
| `FE/src/api/client.ts` (edit) | Real `refreshAccessToken()` (`PUT /auth/refresh`, body `{ deviceGuid, token: <current in-memory token, possibly expired> }`, `credentials: 'include'`); remove the "NOT WIRED YET" comment; `request()` gains the one-shot 401→refresh→retry (R3), with a module-level in-flight promise so concurrent `401`s share one refresh call instead of each starting their own (R7) — reset once the shared promise settles, success or failure; two small listener setters (`setPermissionsListener`, `setSessionExpiredListener`) so `authSession` can react to a refresh that happened *inside* a retried request, without `client.ts` importing React or `shared/` |
| `FE/src/api/errorMessages.ts` (edit) | Add `describeLoginError(error)`, the one place the three raw `error` strings from `AuthController.Login` map to copy (R4's sibling rule for error codes — this is the same idea for the literal strings `AuthController` currently returns, `CLAUDE.md` "the login screen must render copy from the raw `error` string… keyed on that exact string") |
| `FE/src/shared/lib/authSession.ts` | Module-level store: `{ status: 'unknown' \| 'authenticated' \| 'unauthenticated', permissions: string[] }`, plain subscribe/getSnapshot pub-sub (no new state library — `coding-standards.md` §2); `setSession(permissions)`, `clearSession()`; registers itself with `client.ts`'s two listeners at import time. `permissions` is stored and exposed but has no consumer yet — no `useHasPermission`-style hook in this slice (Q2); the next slice that needs permission-gated UI reads this store directly |
| `FE/src/shared/hooks/useAuthSession.ts` | `useSyncExternalStore` wrapper over `authSession`, exposing `{ status, permissions }` |
| `FE/src/shared/hooks/useLogout.ts` | Clears token + session + query cache, navigates to `/login` (R5) |
| `FE/src/features/auth/LoginPage.tsx` | The login screen: form, the four error states (AC2–5), redirect-on-success reading `location.state.from` |
| `FE/src/features/auth/hooks/useLoginMutation.ts` | TanStack `useMutation` wrapping `api/auth.ts#login`, mirrors `useCreateTaskMutation.ts`'s shape |
| `FE/src/app/RequireAuth.tsx` | Route guard: `checking \| authenticated \| unauthenticated` state machine (AC6–9); wraps the existing `AppLayout` route; renders `<Outlet/>` or `<Navigate to="/login" state={{ from }} replace />` |
| `FE/src/app/router.tsx` (edit) | Add the public `/login` route; nest the existing `AppLayout` tree under `RequireAuth` |
| `FE/src/app/AppLayout.tsx` (edit) | Add a "Log out" button in the header, wired to `useLogout` |

No change to `FE/src/api/types.ts`, `FE/src/api/tasks.ts`, `FE/src/api/notifications.ts`, or any `features/board`/`features/task-detail`/`features/notifications` file — the existing Reopen gating (`allowedTransitions`, server-driven) is untouched; this slice adds no client-side permission check.

## UI

### Layout — my own design call, built from the existing visual language

`docs/ui.md` lists login under "Not designed yet," so there is no wireframe to follow. The layout below is new, but every value is a token already in `FE/src/styles/tokens.css` and every pattern (card surface, field/label classes, accent button, danger alert box) is copied from `NewTaskDialog.tsx`, the closest existing form — not invented fresh.

- Full-viewport `bg-bg` background, content centered both axes.
- A single card, `max-width: 360px` (my call — narrower than the 560px dialog, because a login form has two fields), `bg-surface-raised`, `rounded-xl` (`--radius-xl`), `shadow-raised`, padding `--space-5`.
- Card contents, top to bottom: "TaskFlow" as the screen's title (`--font-size-xl` semibold, matching the header's brand treatment in `AppLayout.tsx` sized up one step since there is no app chrome around it); a one-line subtitle "Sign in to see the board" (`--font-size-sm`, `text-text-muted`); the error banner when present (identical markup/classes to `NewTaskDialog`'s `serverError` block: `role="alert"`, `border-danger-border`, `bg-danger-surface`, `text-danger`); username field; password field (both using `NewTaskDialog`'s `fieldClass`/`labelClass` pair, `type="password"` with native browser reveal, no custom show/hide control — nothing in the codebase has one yet and it is not asked for); a full-width primary button, `bg-accent`/`text-on-accent`, label "Sign in" / "Signing in…" while pending (same disabled-while-pending convention as `NewTaskDialog`'s submit button).
- No "forgot password" or "remember me" — out of scope (no such endpoint, no such UI elsewhere).

### States

| State | Shown as |
|---|---|
| Loading (submitting) | Button reads "Signing in…", disabled; both inputs disabled; no spinner overlay (matches `NewTaskDialog`'s plain disabled-button pattern, nothing heavier exists in the codebase to copy) |
| Invalid credentials / will-lock / locked-out | Three distinct messages in the same `role="alert"` banner (AC2–4); password cleared only on invalid-credentials, kept on the lockout variants since retyping the password is not the fix there |
| Network/server error | Same banner, generic copy (AC5); form stays filled in |
| Empty | Does not apply to this screen (no list, no collection) — noted per `frontend.md` §4, not skipped silently |
| Success | Immediate navigation away from the login screen; no success toast needed since the destination screen is the confirmation |
| Denied (route guard, unauthenticated) | `RequireAuth`'s `checking` state renders nothing but a plain "Checking your session…" line (`role="status"`, same `noteClass` pattern `TaskDetailPage.tsx` uses for its own loading state) rather than a blank screen, then either `Outlet` or the redirect |

Widths 390 / 768 / 1440: a single centered card with no internal multi-column layout does not need different behaviour per width; the 360px card simply gets horizontal breathing room on wider screens. Verified by resizing in the browser (see *Verification*), not by a second layout.

## Contract used (already agreed, not changed here)

- `POST /auth/login` — body `{ deviceGuid: Guid, username: string, password: string }` → `ResultRes<{ token: string | null, permissions: string[] }>`; sets the httpOnly `refreshToken` cookie (`path=/api/v1/auth`, `SameSite=Strict`). Failure: `400`, `success: false`, `error` one of the three literal strings in `BE/TaskFlow.Api/Controllers/V1/AuthController.cs#Login` (confirmed by reading `AuthService.Login` — `InvalidCredential`, `WillBeLockedOut`, `IsLockedOut` are mutually exclusive flags on `LoginResultDto`). No `errorCode` — confirmed, `AuthController` never sets `result.ErrorCode`, only `result.Error`.
- `PUT /auth/refresh` — body `{ deviceGuid: Guid, token: string }` (the OLD/expired access token) → same shape, `token`/`permissions`. Requires the `refreshToken` cookie; the controller 400s with `"Invalid tokens"` before calling `AuthService` at all if either `token` or the cookie is empty (confirmed by reading the controller — this is the mechanism behind Q1 below).
- `permissions: string[]` values are the raw permission names (`"ReopenTasks"`, `"DeleteTasks"`, `"ViewTasks"`, …) confirmed in `BE/TaskFlow.Common/Constants/Permissions.cs`/`RolePolicies.cs`.
- No `errorCode`-based mapping exists for auth failures (unlike every `Task*`/`Notification*` endpoint) — this is a known gap in the codebase per `CLAUDE.md`, not something this slice fixes; the client keys off the literal `error` string instead (contract note above).

## Verification plan

**Without a browser (compile-time / static):**
- `npm --prefix FE run typecheck` — new files type-check, `authSession`'s store and `useHasPermission` are correctly typed against `string[]` permissions, `RequireAuth`'s state machine has no `any`.
- `npm --prefix FE run lint` — oxlint, including a new `no-restricted-imports` override for `src/features/auth/**` (mirrors the three existing ones: auth may not import `board`/`task-detail`/`notifications`/`app`).
- `npm --prefix FE run build` — production build succeeds, confirms no circular import between `shared/lib/authSession.ts` and `api/client.ts` (one-directional: `authSession` imports `client`, never the reverse).
- Code-reading check for AC9's "never loops": the retry flag in `request()` is a plain boolean parameter defaulting to `false`, passed as `true` on the single recursive call, so a third `401` on the retried call returns the `ApiError` instead of recursing again — traced by hand, not by a test (no frontend test runner exists yet per `CLAUDE.md` Q-T; adding one is out of scope for this slice and would need its own approval).
- Code-reading check for AC10/R7's single-flight: the in-flight refresh promise is created once and assigned to a module-level variable before the `fetch` to `/auth/refresh` starts, read (not recreated) by any concurrent caller, and cleared in a `finally` once it settles — so a second `401` arriving while a refresh is already running attaches to the same promise rather than calling `refreshAccessToken()` again.
- Code-reading check for AC7: the guard around `PUT /auth/refresh` is a single `if (!hasAccessToken()) { …redirect, return… }` ahead of any network call, so "no token" never reaches `fetch` at all.

**Needs a running dev server / real browser (`npm --prefix FE run dev` against the running API):**
- AC1: manual login with `member1`/`manager1` (Development-seeded, shared password from `DevSeed:Password` — read from the running environment at verification time, never typed into this document).
- AC2: one deliberately wrong password against `member1` or `manager1` — a single failed attempt does not approach the lockout threshold, so this is safe on either seeded account.
- AC3/AC4 (lockout states): **test only against `member2`**, repeating a wrong password until the account locks, and say so before running it — not against `member1`/`manager1`, so their sessions stay usable for every other manual check in this plan without needing an unlock step.
- AC5: toggle the browser offline (devtools "Offline") and submit.
- AC6/7: reload the tab after logging in; confirm via the Network tab that **no** `PUT /auth/refresh` request is sent (AC7's guard skips it outright) and the screen lands on `/login` with the previous path preserved (visit `/tasks/<id>` first).
- AC8/9: log in, wait out the access-token lifetime (or shorten `Security:Jwt:ExpirationMinutes` locally for the session, not committed), then click a board action; confirm one `401` → one `refresh` → the original call succeeding, via the Network tab. For AC9, additionally clear the refresh cookie in devtools before retrying to force the second failure path.
- AC10: with the access token expired, trigger two calls at once (e.g. load a page that fires the board and notifications queries together) and confirm in the Network tab that exactly one `PUT /auth/refresh` fires, not two, and both original calls succeed after it.
- AC11: logout, confirm `/login`, confirm a back-navigation doesn't show stale board data (cache cleared).
- AC12: keyboard-only pass (Tab, Shift+Tab, Enter) and a screen reader spot-check that the error banner is announced.
- AC13: after login, check the Application/Storage panel for the `refreshToken` cookie (httpOnly, `SameSite=Strict`, `path=/api/v1/auth`) and confirm the Network tab shows it on the `PUT /auth/refresh` request made during AC8.
- Widths 390/768/1440 per `frontend.md` §7, resizing the viewport on the rendered login screen and the "Checking your session…" guard state.

## Open questions — resolved

- **Q1 — reload cannot silently restore a session under today's contract. Decided: accept, no backend change in this slice.** `PUT /auth/refresh` requires the old access token in the body (`RefreshTokenReq.Token`), and the controller rejects an empty one with `400 "Invalid tokens"` before it ever reaches `AuthService` or looks at the cookie. The access token is required (R1) to live only in memory, so a real page reload has already lost it — there is nothing valid to put in that field. User decision: re-entering credentials after a reload is acceptable for the MVP; `AuthController` is not touched on this branch. AC7 skips the doomed `PUT /auth/refresh` call outright rather than sending and ignoring it. Tracked as a backend follow-up in `docs/ROADMAP.md` ("Backend follow-ups raised by slice 1.8"), to be designed with `architect` since auth is a risk area, combined with Q3 into one future backend slice.
- **Q2 — no second consumer for permission gating yet. Decided: drop `useHasPermission` and AC10 from this slice.** `authSession` still stores and exposes `permissions[]` from login/refresh, but this slice adds no gating hook and no new gated affordance. `ReopenTasks` keeps its existing, correct server-driven gating through `allowedTransitions`, untouched. Whichever later slice needs client-side permission gating (e.g. a delete-task UI) reads `permissions` from the existing store and adds its own hook then.
- **Q3 — no logout endpoint. Decided: accept client-only logout for this slice.** `AuthController` has only `login`, `refresh`, `check`. Logout here forgets the in-memory token and the client's own notion of the session but cannot revoke the `UserToken`/refresh cookie server-side — the cookie stays valid until it naturally expires. Tracked in `docs/ROADMAP.md` alongside Q1 as one combined future backend slice (`POST /auth/logout`).
- **Assumption** — `username`/`password` have no documented length/charset limits in `LoginReq` (no `[MaxLength]`/`[Required]` attributes on that request model, confirmed by reading it), so the login form applies no client-side length cap beyond `required` on both fields; the server is the only enforcer here, consistent with `coding-standards.md` §4's "UI ≤ API" rule applied to "API has no stated limit, so UI adds none beyond what HTML's `required` gives for free."
- **Assumption** — `authSession`'s store is a hand-rolled `useSyncExternalStore` pub-sub rather than a new dependency, per `frontend.md` §2 "No new UI library, state manager" and `coding-standards.md` §2 "take the first option that fully works… something already in this codebase [React itself] → … the minimum new code." Flagging in case the team would rather see this as a decision to confirm rather than an assumption.
