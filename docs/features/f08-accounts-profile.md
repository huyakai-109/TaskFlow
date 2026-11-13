# F08 — Accounts (Admin) and self profile with avatar
Status: **Signed off 2026-11-13** (A4, A8, A9, A11 in [round-2-decisions.md](round-2-decisions.md), with the additions below) · ADR [0006](../adr/0006-account-administration-and-avatar-storage.md) · Overrides `CLAUDE.md` Q-U "User management stays out of scope"

## Permission matrix
| Action | Member | Manager | Admin |
|---|:--:|:--:|:--:|
| View user list (`GET /users`), avatars, workload | ✓ | ✓ | ✓ |
| Read/update own display name, avatar, password | ✓ | ✓ | ✓ |
| Create user, change role, deactivate/activate, reset password, view admin detail, remove anyone's avatar | — | — | ✓ (`ManageUsers`) |

Manager's legacy `Members.*` claims grant nothing — no endpoint uses them (F01 R2).

## Data (migration `AddUserProfileAndAccountState`, Additive)
`Users.DisplayName varchar(100) NULL` (fallback `userName`) · `AvatarObjectKey varchar(200) NULL` · `DeactivatedAt datetime(6) NULL` (null = active) · `MustChangePassword tinyint(1) NOT NULL DEFAULT 0`. Deactivation is **not** `IsDeleted` (which would hide names on tasks/history) and **not** lockout (the failed-login mechanism). `UserName` is immutable. No email is collected for new users.

## Admin API (`UsersController`, policy `ManageUsers`)
| Verb route | Request | Success | Errors |
|---|---|---|---|
| POST /users | `userName` [Required] `^[A-Za-z0-9._-]{3,50}$` · `displayName` ≤100 · `role` [Required][EnumDataType] Admin\|Manager\|Member | `ResultRes<UserCreatedRes { user: UserAdminRes, temporaryPassword }>`, `Cache-Control: no-store` | User.Create.UserNameTaken · .Exception |
| GET /users/{id} | — | `ResultRes<UserAdminRes { id, userName, displayName, role, isActive, deactivatedAt, mustChangePassword, isLockedOut, createdAt, avatarUrl }>` | User.Get.NotFound · .Exception |
| PUT /users/{id}/role | `role` [Required][EnumDataType] | `ResultRes<UserAdminRes>` | User.ChangeRole.NotFound · .LastAdmin · .Conflict · .Exception |
| PUT /users/{id}/deactivate | — | `ResultRes<UserAdminRes>`, idempotent; revokes refresh tokens (deletes the user's `UserTokens` rows), updates SecurityStamp | User.Deactivate.NotFound · .Self · .LastAdmin · .Conflict · .Exception |
| PUT /users/{id}/activate | — | idempotent | User.Activate.NotFound · .Conflict · .Exception |
| PUT /users/{id}/password-reset | — | `ResultRes<{ temporaryPassword }>`, no-store; sets MustChangePassword, clears lockout, revokes refresh tokens | User.ResetPassword.NotFound · .Conflict · .Exception |
| DELETE /users/{id}/avatar | — | ExecutionRes | User.RemoveAvatar.NotFound · .Exception |

**Role changes** (promote/demote) reach existing access tokens at the next refresh (≤15 min) — accepted (open decision 22), unchanged by the addition below. Identity writes rotate `ConcurrencyStamp`, which can make a concurrent F02 capacity write return `Conflict` (ADR 0002 consequence, unchanged).

### Quick revocation on deactivation — signed off 2026-11-13
Deactivation (unlike a role change) must take effect **quickly**, not wait for the access token's natural ≤15 min expiry, because it is usually a response to an incident (an account compromised, someone leaving). Mechanism: `PUT /users/{id}/deactivate` writes a Redis key `deactivated:{userId}` (no expiry, removed by `/activate`) in the same handler that sets `DeactivatedAt`, **before** committing the DB write succeeds is reported to the caller — if the Redis write fails, the whole operation fails and rolls back (deactivation must not silently stay DB-only). A new piece of `JwtBearerEvents.OnTokenValidated` checks that key (one Redis `EXISTS`, sub-millisecond, run on every authenticated request — acceptable cost, Redis is already in the stack for the distributed cache) and fails validation (401) if present, instead of trusting the token until it expires. This also governs SignalR: a live hub connection is validated once at negotiation, not per message, so the same deactivation handler also asks `IPresenceTracker` for every connection id belonging to that user and aborts them directly (`HubContext.Abort(connectionId)`) in the same request — this is what makes [f07-presence.md](f07-presence.md) AC9 "near-immediate" rather than bounded by token expiry. Reactivation removes the Redis key; the user's *next* login issues a fresh token as normal (existing refresh tokens were already revoked by `/deactivate`, per the table above).

### Tasks held by a deactivated user — signed off 2026-11-13
**Default: nothing changes about the tasks themselves.** A deactivated user's existing assignments are left exactly as they are — the task stays assigned to them, their name keeps rendering everywhere (board, history, Activity), and the task still counts toward their capacity (F02's count has no "is this user active" condition, and adding one would let a Manager's reassignment of someone else's task behave differently depending on an unrelated user's state — not done). What *does* change: that user can no longer be chosen as a **new** assignment target (`AssigneeNotFound` — f02 R2 AC17 extension) and, being deactivated, cannot log in to act on the task themselves. In practice a Manager reassigns or reopens around them; no automatic reassignment is performed by the deactivate action itself (that would be a side effect on task data from an account-management action, and is explicitly not requested).

## Self API (`ProfileController`, route `api/v1/me`, policy `ViewTasks`)
| Verb route | Request | Success | Errors |
|---|---|---|---|
| GET /me | — | `ResultRes<MeRes { id, userName, displayName, role, avatarUrl }>` | Profile.Get.Exception |
| PUT /me | `displayName` ≤100, no control characters, null/blank clears | `ResultRes<MeRes>` | Profile.Update.Conflict · .Exception |
| PUT /me/password | `currentPassword` [Required] · `newPassword` [Required] | ExecutionRes; other sessions' refresh tokens revoked | Profile.ChangePassword.WrongPassword · .PolicyViolation (validates[] from IdentityErrors) · .Exception |
| PUT /me/avatar | multipart field `file` | `ResultRes<{ avatarUrl }>` | Profile.Avatar.Empty · .TooLarge · .UnsupportedType · .StorageUnavailable · .Conflict · .Exception |
| DELETE /me/avatar | — | ExecutionRes | Profile.Avatar.Exception |
| GET /users/{id}/avatar?v= | (ViewTasks, in UsersController) | image bytes | User.Avatar.NotFound (400 envelope) |

## Auth changes (`AuthControllerMS.cs`)
- `POST /auth/login`: deactivated → the existing generic invalid-credentials response (no account-state oracle). `MustChangePassword` with a correct password → `success:false`, `errorCode: Auth.Login.PasswordChangeRequired`, no tokens.
- `POST /auth/password/change-required` **[AllowAnonymous]** `{ userName, currentPassword, newPassword }` → uses `CheckPasswordSignInAsync(lockoutOnFailure: true)`; succeeds only when the password is right **and** `MustChangePassword` is set (otherwise the generic invalid-credential code); clears the flag. Codes `Auth.ChangeRequired.InvalidCredential` · `.PolicyViolation` · `.Exception`. **Review item: new anonymous endpoint.**
- `PUT /auth/refresh`: refused (generic) when deactivated or `MustChangePassword`.

## Password policy
`IdentityOptions.Password.RequiredLength` from new key `Security:Password:RequiredLength` (default 10); Identity's digit/upper/lower/non-alphanumeric defaults kept. Temporary passwords: 16 characters from `RandomNumberGenerator`, guaranteed to satisfy the policy, returned once, never stored in plain text, never logged. Request/response models carrying passwords are never logged or destructured; responses carrying a temporary password are `Cache-Control: no-store`. **Before raising the length, check that `DevSeed:Password` satisfies it** — `DevelopmentSeedConfiguration` throws at startup otherwise.

## Dev seed and admin
`DevelopmentSeedConfiguration`: no change required (`MustChangePassword` defaults false; display names optional). The migration-seeded `admin` has a well-known dev password documented in `UserConfiguration.cs`; any non-development deployment must force its change, as a `/ship setup` step, not a migration.

## `GET /users` (existing, ViewTasks) — additive
`{ id, userName, role }` + `displayName`, `avatarUrl`, `isActive`. Deactivated users are now listed (`isActive:false`) so historic names render; assignee pickers and presence filter `isActive`. Soft-deleted users stay excluded; order unchanged. Task payload `assignee` + `displayName`, `avatarUrl`. Initials fallback = first two characters of `displayName ?? userName`. Shown on: board card, presence stack, sidebar footer, assignee pickers, history/activity actor, team capacity, notification actor.

## Avatars on the existing `StorageService`
- Do **not** use `IStorageService.Upload(UploadFileReqDto)`: it keeps the user's filename, derives the type from the extension (incl. SVG) and checks no size. Add `PutObject(string key, Stream content, long size, string contentType, CancellationToken)` and `GetObject(string key, CancellationToken)` → `(Stream, contentType, length)`; reuse `Remove`. Bucket existence is checked once at startup, not per call.
- Private bucket: new key `MinIO:PrivateBucket` (no anonymous policy). The current `dataset` bucket is documented as anonymous-read and must not hold avatars.
- Limits: `[RequestSizeLimit(2_200_000)]`, `[RequestFormLimits(MultipartBodyLengthLimit = 2_097_152)]`, `0 < file.Length ≤ 2 MiB`.
- Type: sniff the first 12 bytes — JPEG `FF D8 FF`, PNG `89 50 4E 47 0D 0A 1A 0A`, WebP `RIFF????WEBP`. The client's Content-Type and filename are ignored. SVG/GIF/BMP/ICO refused.
- Key: `avatars/{userId}/{Guid:N}.{jpg|png|webp}` from the sniffed type — no user input in the key, so no path traversal.
- Flow: validate → put the new object → tracked user update (`AvatarObjectKey`), one `SaveChanges`, two-catch → on failure, remove the new object and return `Conflict` → after commit, best-effort remove the old object (`Warning` on failure; orphan accepted).
- Serving: `GET /api/v1/users/{id}/avatar?v={guid}` streams from MinIO with the stored type; `Cache-Control: private, max-age=31536000, immutable` and `ETag: "{v}"` when `v` matches the current key, else `Cache-Control: no-cache` and no `ETag`; `X-Content-Type-Options: nosniff`; `Content-Security-Policy: default-src 'none'; sandbox`; `Content-Disposition: inline`. **Signed off 2026-11-13:** the endpoint also honours `If-None-Match` — when it matches the current `ETag`, the response is `304 Not Modified` with no body, so a client that already has the current version avoids re-downloading it even without the `v`-keyed cache below. `avatarUrl` = that path, or null. The client fetches with the bearer token (the token is memory-only, so a bare `<img src>` cannot authenticate), turns it into an object URL, and caches by `(userId, v)` with `staleTime: Infinity`.
- No server-side re-encoding in v1 (no image library). The client crops/resizes to 256×256 before upload (also strips EXIF in practice; never trusted server-side).

## Anonymous MinIO upload endpoint — closed in this slice (moved out of "deferred" 2026-11-13)
`POST /example/minio/upload` (`ExampleController.cs`) is `[AllowAnonymous]` and writes into the public `dataset` bucket — pre-existing, found during the round-2 review, not introduced by it. The product owner asked for this to be audited and closed in the same slice as the new, correctly-authenticated avatar path, not deferred to `/ship setup`. Work for R2-3/R2-6: (1) grep both `BE/` and `FE/` for any caller of `/example/minio/upload` or `/example/cache/test` (the latter is also `[AllowAnonymous]`); (2) if nothing calls it in product code (expected — it is demo/scaffold code per `CLAUDE.md`'s "scaffold code that is not a pattern" list), delete the action; if something unexpectedly depends on it, put it behind `[Authorize]` instead of deleting it, and say so before doing it, since that would be a scope surprise; (3) either way, add a regression test (a request to the route is 401/404, never 200) so it cannot silently come back. This does **not** remove the `ExampleController` itself or its other demo actions — only this one anonymous, publicly-writable upload path and its MinIO-cache sibling are in scope here.

## Acceptance criteria (summary)
Admin creates a Member → temporary password shown once; login with it → `PasswordChangeRequired`; change-required endpoint → then normal login works. Member/Manager calling any admin endpoint → 403. The last active Admin cannot be demoted or deactivated; an Admin cannot deactivate themselves. A deactivated user cannot log in or refresh, stays listed with `isActive=false`, and cannot be assigned (`AssigneeNotFound`). Avatar: 2 MiB + 1 byte → `TooLarge`; a PNG renamed `.jpg` is stored as PNG; an SVG or HTML file renamed `.png` → `UnsupportedType`; the object key never contains the uploaded filename; replacing an avatar removes the old object; another user's avatar is readable by any `ViewTasks` holder and not anonymously. No password or temporary password appears in any log.