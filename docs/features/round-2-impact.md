# Contract round 2 — impact table, final API list, slice plan, open decisions, risks

**All 36 open items are signed off as of 2026-11-13** — see [round-2-decisions.md](round-2-decisions.md) for the condensed A/B/C decision record (A1–A11 group-A items including the final A2, B1–B13 accepted in full, C1–C4/C6/C7 deferred, former C5 moved into the R2-3/R2-6 slice). This file's §5 "open decisions" below is kept as the detailed rationale behind each recommendation; `round-2-decisions.md` is the authoritative record of what was actually decided.

Written by `architect` (2026-11-13), assembled from its two review passes. Companion to the per-feature revisions in [f01](f01-workflow.md)/[f02](f02-capacity.md)/[f03](f03-sorting.md)/[f04-audit](f04-audit.md)/[f05](f05-overdue.md), the new specs [f06](f06-notification-history.md)/[f07](f07-presence.md)/[f08](f08-accounts-profile.md)/[f10](f10-read-models-and-sort.md), the product-manager/architect Activity spec [f04-activity-feed.md](f04-activity-feed.md), and ADRs [0004](../adr/0004-signalr-for-presence.md)–[0007](../adr/0007-keyset-paging-for-activity-feed.md). **No migration has been applied; nothing here has been implemented.**

## 0. What the code actually said, checked before designing (not assumed from old specs)

| Claim the brief/old specs make | What the code says | Evidence |
|---|---|---|
| "Read notifications disappear" | The server never deletes them. `MarkRead` sets `IsRead=true`, `ReadAt=now`. The bell used to **filter** to `isRead=false` — a client bug, since fixed. | `NotificationService.cs:59-77`, `useNotificationsQuery.ts` |
| Sidebar unread badge | **Defect:** counted `result.length` of a page capped at 20, not the real unread total. | `Sidebar.tsx:149` |
| `allowedTransitions` per caller | Already computed per calling user. | `TaskTransitions.AllowedFor`, `TaskService.cs` |
| Members may move/assign any task | True today. Move only checks the D5 "not assignee" edges; Assign has no caller rule at all. | `TaskService.cs` |
| Priority / due date | Policy `UpdateTasks`, which Members hold. | `TasksController.cs` |
| "Only Admin may manage users" | Manager already holds `ViewMembers/CreateMembers/UpdateMembers/DeleteMembers` (claims 1001–1004); no controller uses them. | `RolePolicies.cs` |
| Deactivated users | Not a state today. Only `IsDeleted` and Identity lockout exist. | `User.cs`, `UserConfiguration.cs` |
| Password policy | Only lockout is configured; Identity's password-complexity defaults apply. | `SecurityConfiguration.cs` |
| `StorageService` | MinIO, one flat bucket `dataset`, documented as anonymous-read; object name keeps the user's filename; content type from the extension (incl. SVG/ICO/BMP); no size check. | `StorageService.cs`, `GlobalConstants.cs`, `appsettings.json` |
| Anonymous upload | `POST /example/minio/upload` is `[AllowAnonymous]`, writes into the public bucket — pre-existing, **must be removed before any deployment**. | `ExampleController.cs` |
| Board "Sort" button | A static label, not a control. | `BoardPage.tsx` |
| SignalR | Not used; ships in the shared framework server-side (no NuGet), needs `@microsoft/signalr` client-side (new npm dependency). | `*.csproj`, `FE/package.json` |
| F01 "D8" | Referenced elsewhere in F01 but missing from its own Decisions list. | `f01-workflow.md` (fixed by R2) |
| `CLAUDE.md` stale facts | Status, `InvariantGlobalization` (now false), "No CI" (CI exists), "no endpoint creates users" — all stale; diff proposed in ADR 0005. | — |

## 1. Impact table: every changed decision → old spec/AC

| Change | File · item | Effect | Note |
|---|---|---|---|
| Holder-only (F01 D11) | f01 D8 (now written down) | **Split** | Visibility half unchanged; "Members may move/assign any task" revoked |
| Holder-only | f01 D5 | Extended, not replaced | D5 still governs Review exits; new D11 governs entry into InProgress/Review |
| Holder-only | f01 D3 validation order | Modified | existence → version → same-status → transition → actor rules (D5, D11) → assignee resolvable → capacity |
| Holder-only | f01 transition table, AC2, AC3 | Rewritten | "when **its assignee** moves it…"; new AC2b/AC3b for a non-assignee refusal |
| Holder-only | f01 AC17/AC18, AC20/AC27/AC28, AC29 | Extended/reworded | New codes distinguished; AC29 reads "by the assignee (two tabs)" |
| Holder-only | f01 permission table | Modified | + `PlanTasks`, `AssignAnyone`, `ManageUsers` |
| Holder-only | f01 assignee endpoint (T5 #8) | Modified rules | New codes `Task.Assign.SelfOnly`, `Task.Assign.Locked` |
| Holder-only | f02 AC1–AC10, AC13–AC15, AC17, AC18, AC22 | Reworded/modified | Mover/assignee language throughout; AC13 → `Locked`; AC15 rewritten (limit applies to the assignee, not the requester) |
| Holder-only | SPEC §5.6 dialog 2, COMING-SOON C2 | Reinforced | "Assign and start for someone else" stays out of scope |
| Holder-only | SPEC §10 point 8 / `AssignAndMoveDialog` | Modified | Becomes "Take it and start" (self only) |
| Planning rights (F01 D13) | f01 T5 #1 `POST /tasks` | Modified | `Priority` optional, default `Normal`; refusal (not silent drop) without `PlanTasks`/`AssignAnyone` |
| Planning rights | f01 T5 #6/#7 | Modified | Policy `UpdateTasks` → `PlanTasks` |
| Planning rights | f03 ordering | Consequence | Member-created tasks rank last until triaged |
| Planning rights | f05 AC7, AC9, AC12–AC15 | Reworded/modified | Actor must hold `PlanTasks`; AC15 → 403 for every Member |
| Flag cleared withdrawn | f05 T9 "[SIGN-OFF] Not built: Flag cleared this week" | **Withdrawn permanently** | No `OverdueCleared` event, ever |
| Flag cleared withdrawn | SPEC §5.7 group 3, §7 bullet, §10 point 4 | Removed | |
| Flag cleared withdrawn | COMING-SOON B1 | Withdrawn | Stub removed, not replaced |
| Notification history | f01 T1/T5 #13–14, T3 codes | Extended (additive) | `ActorUserId`; response gains `task`, `actor`; new `PUT /notifications/read-all` |
| Notification history | COMING-SOON A1 Inbox | Replaced | By f06 bell history + f04-activity-feed.md |
| Real-time scope | `CLAUDE.md` out-of-scope "real-time push" | **Scope change** (ADR 0005) | Presence in v1; board/notification push is v2 |
| Real-time scope | COMING-SOON A9 | Replaced | Presence stack; capacity pill kept |
| Accounts/profile | `CLAUDE.md` Q-U, "Users" bullet | **Scope change** | Admin-only account management + self profile |
| Accounts/profile | f02 T4 `GET /users` | Extended (additive) | + `displayName`, `avatarUrl`, `isActive`; deactivated users now listed |
| Accounts/profile | f01-login.md / login, refresh | Modified | `Auth.Login.PasswordChangeRequired`; deactivated users refused generically |
| Board sort | f03 D1 "no client-supplied sort parameter" | **Overridden** | A server-defined enum, not arbitrary columns |
| Board sort | f03 AC1–AC15 | Qualified | Hold under `sort=priority` (default) |
| Read models | COMING-SOON B2–B6, SPEC §10.9, ROADMAP R15 | **Resolved** | See f10 §2–6 |
| Activity feed | f04-audit.md out-of-scope "a cross-task activity feed" | **Lifted** | Superseded by f04-activity-feed.md |
| Activity feed | f04-audit.md T1 indexes | Extended (additive) | `AddTaskHistoriesFeedIndexes` |

## 2. Migrations (all additive, all with a clean `Down()`, `MyDbContext` only, never Quartz)

| # | Name | Contents |
|---|---|---|
| M-A | `AddRound2Permissions` | `RoleClaim` inserts: Admin 13 `PlanTasks`, 14 `AssignAnyone`, 15 `ManageUsers`; Manager 1013 `PlanTasks`, 1014 `AssignAnyone` |
| M-B | `AddTaskNotificationActorAndInboxIndex` | `TaskNotifications.ActorUserId bigint NULL` FK → Users Restrict; `IX_TaskNotifications_RecipientUserId_IsRead_CreatedAt_Id` |
| M-C | `AddUserProfileAndAccountState` | `Users.DisplayName`, `AvatarObjectKey`, `DeactivatedAt`, `MustChangePassword` |
| M-D | `AddTaskHistoriesFeedIndexes` | `IX_TaskHistories_CreatedAt_Id`, `IX_TaskHistories_ActorUserId_CreatedAt_Id` |

None rewrites or drops data. Applying any of them, even locally, needs the user's approval (`AutoMigration=true`).

## 3. Final consolidated API list (round 2)

| # | Verb route | Policy | Status | Slice |
|---|---|---|---|---|
| 1 | POST /tasks | CreateTasks | changed (priority optional, planning/assignee rules) | R2-1 |
| 2 | GET /tasks/{id} | ViewTasks | changed (+canTakeOver, canAssignOthers, requiresTakeover; later +lastTransition, assignee displayName/avatarUrl) | R2-1/5/3 |
| 3 | GET /tasks/board | ViewTasks | changed (+sort, echo) | R2-1/5 |
| 4 | GET /tasks?status&isOverdue&assigneeId&sort&start&length | ViewTasks | changed (additive) | R2-5 |
| 5 | PUT /tasks/{id} | UpdateTasks | unchanged | — |
| 6 | PUT /tasks/{id}/priority | **PlanTasks** | **changed (breaking for Members)** | R2-1 |
| 7 | PUT /tasks/{id}/due-date | **PlanTasks** | **changed (breaking for Members)** | R2-1 |
| 8 | PUT /tasks/{id}/assignee | AssignTasks | changed rules (SelfOnly, Locked) | R2-1 |
| 9 | PUT /tasks/{id}/status | MoveTasks | changed rules (ActorNotAssignee) | R2-1 |
| 10 | PUT /tasks/{id}/reopen | ReopenTasks | unchanged | — |
| 11 | DELETE /tasks/{id} | DeleteTasks | unchanged | — |
| 12 | GET /tasks/{taskId}/history | ViewTasks | unchanged (later +actor displayName) | — |
| 13 | GET /notifications | ViewTasks | changed (+task, +actor) | R2-2 |
| 14 | PUT /notifications/{id}/read | ViewTasks | unchanged | — |
| 15 | PUT /notifications/read-all | ViewTasks | **new** | R2-2 |
| 16 | POST /auth/login | anonymous | changed (PasswordChangeRequired, deactivated) | R2-3 |
| 17 | PUT /auth/refresh | anonymous (cookie) | changed (deactivated/must-change refused) | R2-3 |
| 18 | POST /auth/password/change-required | **anonymous** | **new** | R2-3 |
| 19 | GET /users | ViewTasks | changed (+displayName, avatarUrl, isActive; inactive listed) | R2-3 |
| 20 | GET /users/{id} | ManageUsers | **new** | R2-3 |
| 21 | POST /users | ManageUsers | **new** | R2-3 |
| 22 | PUT /users/{id}/role | ManageUsers | **new** | R2-3 |
| 23 | PUT /users/{id}/deactivate · /activate | ManageUsers | **new** | R2-3 |
| 24 | PUT /users/{id}/password-reset | ManageUsers | **new** | R2-3 |
| 25 | GET /me · PUT /me · PUT /me/password | ViewTasks | **new** | R2-3 |
| 26 | GET /activity | ViewTasks | **new** — `CursorResultRes`, keyset paging (ADR 0007) | R2-4 |
| 27 | GET /workflow | ViewTasks | **new** | R2-5 |
| 28 | GET /users/workload | ViewTasks | **new** | R2-5 |
| 29 | GET /me/summary | ViewTasks | **new** | R2-5 |
| 30 | PUT /me/avatar · DELETE /me/avatar | ViewTasks | **new** | R2-6 |
| 31 | GET /users/{id}/avatar?v= | ViewTasks | **new** | R2-6 |
| 32 | DELETE /users/{id}/avatar | ManageUsers | **new** | R2-6 |
| 33 | hub /hubs/presence (Join, Leave, PresenceChanged) | ViewTasks | **new** | R2-7 |
| — | POST /example/minio/upload, /example/cache/test (anonymous scaffold) | — | **closed in this round** (moved out of deferred 2026-11-13 — audited and removed/authorized in R2-3/R2-6, see f08-accounts-profile.md) | R2-3/R2-6 |

## 4. Slice plans

**Backend (sequential, one working tree). Order follows the product owner's stated priority: permissions → notifications → accounts → Activity, then read models, avatars, presence.**

| Slice | Content | Migration | Blocks / blocked by | Required tests |
|---|---|---|---|---|
| **R2-1** Permissions and workflow rules | M-A; `TaskTransition.ActorMustBeAssignee`; Move/Assign/Create rules; `PlanTasks` on priority/due-date; `requiresTakeover`, `canTakeOver`, `canAssignOthers`; 5 new codes | M-A, Additive | Blocks R2-3 (ManageUsers seeded here) and FE-1 (**breaking contract — must ship together**) | Rewrite existing Move/capacity/Assign tests to act as the assignee; new AC2b/2c/3b/3c/32–37 and F02 AC23–25; validation-order tests; AC29 with two connections of the same user; 403 for Members on priority/due-date; `allowedTransitions` matrix per role × status × holder |
| **R2-2** Notification history | M-B; actor written on rework; additive response fields; read-all | M-B, Additive | Blocks FE-2, and the Inbox removal in FE-4 | F06 AC1–8; rework-commit atomicity unchanged (F01 AC31); query count for the list |
| **R2-3** Accounts and profile (no avatars) | M-C; `UsersController` admin actions; `ProfileController`; auth changes; password options; inactive users in `GET /users`; `AssigneeNotFound` for deactivated | M-C, Additive | Needs R2-1 (`ManageUsers`). Blocks R2-6, FE-3 | F08 ACs; last-admin and self-deactivate guards; login/refresh for deactivated and must-change users; anonymous endpoint lockout; no secrets in logs; DevSeed still seeds under the new policy |
| **R2-4** Activity feed | M-D; `GET /activity` with keyset paging (ADR 0007); `GetFeed` in `TaskHistoryService` | M-D, Additive | After R2-3 (actor displayName available). Blocks FE-4 | f04-activity-feed.md AC1–25 incl. cursor stability under concurrent inserts (AC8), VN date boundaries, `EXPLAIN` recorded |
| **R2-5** Read models and sort | `sort` enum; `assigneeId`; `lastTransition`; `/workflow`; `/users/workload`; `/me/summary` | none | Independent; blocks FE-5 | Stable paging per sort; `Done` ignores sort; `lastTransition` per status incl. reopen; batched query count; week boundaries in VN zone; workload equals the F02 count |
| **R2-6** Avatars | `StorageService.PutObject`/`GetObject`; private bucket key; avatar endpoints | none (uses M-C) | Needs R2-3. Blocks FE-6 | Magic-byte table (valid, renamed, SVG/HTML-as-png, empty, oversize); key never contains the filename; old object removed; failure compensation; response headers asserted |
| **R2-7** Presence | Hub, tracker, JWT query handling limited to `/hubs`, log pins, `CloseOnAuthenticationExpiration` | none | Needs ADR 0004/0005 approval; better after R2-3 (displayName) | Tracker unit tests (multi-tab, disconnect, limits); hub integration test (test-only `Microsoft.AspNetCore.SignalR.Client`); log-capture test that no `access_token` is logged |

**Frontend**

| Slice | Content | Depends on |
|---|---|---|
| FE-0 | Remove the B1 "Flag cleared" stub and its copy | none (can ship now) |
| FE-1 | Permission-aware UI: Priority/Due read-only without `PlanTasks`; "Take over" action; `AssignAndMoveDialog` → "Take it and start" (self); Members' picker = self only; copy for the 5 new error codes; `requiresTakeover` drop flow | R2-1 (ships together) |
| FE-2 | Bell: unread count from the real total (fixes the 20-cap defect), "Mark all read" with `upToId` | R2-2 |
| FE-3 | Admin Users page (list, create with one-time password reveal, role, deactivate/activate, reset); Profile page (display name, password); forced-change screen in the login flow; displayName everywhere with initials fallback | R2-3 |
| FE-4 | Activity page (filters, cursor-based paging, Refresh); nav swap; delete InboxPage/hooks; `*` not-found route | R2-4 and FE-2 shipped |
| FE-5 | Sort menu (URL param, keyed queries); `/workflow` replaces `IN_PROGRESS_LIMIT` and fills B3/B4; workload replaces client capacity derivation; My tasks via `assigneeId`; Approved/Sent back/Submitted by; finished-this-week | R2-5 |
| FE-6 | Avatar upload (crop/resize 256, client pre-check); authenticated `<Avatar>` (fetch blob, cache by version) | R2-6 |
| FE-7 | Presence: `@microsoft/signalr` (needs approval), connection provider in `app/`, board presence stack replacing AvatarStack/TeamCapacityPopover, task-detail viewers | R2-7 |

Deploy note for R2-1: sessions issued before M-A lack `PlanTasks`/`AssignAnyone` until their next refresh (≤15 min); Managers briefly get 403 on priority/due-date. Accepted, or ask users to log in again.

## 5. Open decisions for the product owner (each with the architect's recommendation)

1. Holder-only applies to **both** `Todo→InProgress` and `InProgress→Review`. **Rec: yes.**
2. No Manager/Admin bypass of holder-only. **Rec: no bypass** (matches F02 D4's "no role bypass" spirit).
3. `Backlog→Todo` stays open to anyone with `MoveTasks`. **Rec: yes.**
4. Members: self-takeover and release-own only; Manager/Admin assign anyone (`AssignAnyone`). **Rec: yes.**
5. Assignment locked in `Review` and `Done` for every role (closes the self-approval loophole). **Rec: yes.**
6. An unassigned Todo drop onto InProgress offers "take it and start" (`requiresTakeover`). **Rec: yes.**
7. `PlanTasks` held by Manager **and Admin** (the directive said "only Manager"). **Rec: include Admin.**
8. A Member's create request carrying priority, due date or someone else as assignee is **refused**, not silently ignored. **Rec: refuse.**
9. Keep Manager's dead `Members.*` claims (no renumbering); new `ManageUsers` is Admin-only; cleanup later. **Rec: yes.**
10. "Flag cleared" withdrawn everywhere. **Rec: yes.**
11. No notification TTL/retention in v1. **Rec: yes.**
12. Approve `PUT /notifications/read-all` (bulk action on one's own notifications — "bulk operations" out-of-scope meant tasks). **Rec: approve.**
13. Notification `ActorUserId` without backfill (legacy rows show no name). **Rec: yes.**
14. Scope change: real-time push in scope, staged as presence v1 with board/notification push as v2 (ADR 0005). **Rec: approve v1 only.**
15. Adopt SignalR; new npm dependency `@microsoft/signalr`. **Rec: approve.**
16. Presence scopes: board **and** task detail. **Rec: both.**
17. Token transport: `access_token` query string with log pinning now, or a one-time ticket endpoint. **Rec: query string now; ticket if a logging proxy appears at `/ship setup`.**
18. Single-instance in-memory presence; Redis backplane deferred. **Rec: yes.**
19. Presence stack replaces the board AvatarStack and Team popover; keep the "N at capacity" pill. **Rec: keep the pill.**
20. Presence privacy: no opt-out, nothing persisted. **Rec: yes.**
21. Deactivation as a new `DeactivatedAt` column (not soft delete, not lockout). **Rec: yes.**
22. Accept ≤15 min of stale role/activation in live access tokens. **Rec: accept.**
23. Temporary passwords server-generated, shown once, forced change through the anonymous lockout-protected endpoint. **Rec: yes.**
24. Password minimum length 10 plus Identity's complexity defaults, configurable. **Rec: yes**, after confirming `DevSeed:Password` complies.
25. The seeded `admin`'s known dev password must change in any non-dev deployment, as a `/ship setup` step. **Rec: yes.**
26. Username immutable, no email collected. **Rec: yes.**
27. Avatars: JPEG/PNG/WebP ≤2 MiB, magic-byte sniffing, client-side crop to 256 px, no server re-encode in v1. **Rec: yes.**
28. Avatars served through the authenticated API from a **private** bucket, not presigned/public URLs. **Rec: yes.**
29. Admin can remove anyone's avatar. **Rec: yes.**
30. Sort options: Priority (default), Due date, Newest; kept in the URL, not persisted server-side. **Rec: yes.**
31. "This week" = Monday–Sunday in Asia/Ho_Chi_Minh for `/me/summary`. **Rec: yes.**
32. Activity feed: lift F04's out-of-scope line, visible to every role, full depth, no badge, reload-on-focus + manual Refresh (no polling), replacing Inbox with no redirect. **Rec: approve all** (per the user's own Activity spec, [f04-activity-feed.md](f04-activity-feed.md)).
33. Remove the Inbox page only after bell history (FE-2) ships.
34. **ADR 0007 — keyset paging for `GET /activity`**, a second paging style next to the codebase's offset default. **Rec: approve**, scoped to append-only newest-first streams only.
35. Members keep `UpdateTasks` (edit title/description of **any** task), which the directive did not mention. **Rec: unchanged; confirm.**
36. Test-only dependency `Microsoft.AspNetCore.SignalR.Client` for hub integration tests. **Rec: approve when R2-7 starts.**

## 6. Risks that matter

- **R2-1 is a breaking contract change.** Members lose priority/due-date (403) and moving tasks for others. F02 AC13's error code changes (now `Locked`). It must ship together with FE-1, and a large part of the existing backend test suite must be rewritten to act as the assignee — do not weaken tests to get green.
- **Self-approval loophole** if open decision 5 is rejected: A submits, B takes over, A approves their own work. Mitigation is decision 5 itself (locking assignment in Review/Done).
- **Token in the URL** for SignalR: today's only protection is an incidental `Microsoft: Error` log override — pin it explicitly (R2-7).
- **Anonymous change-required endpoint**: lockout applies via `CheckPasswordSignInAsync(lockoutOnFailure: true)`, giving a generic response for every non-success case — still a new anonymous surface to review.
- **Public MinIO bucket and the anonymous `/example/minio/upload`**: pre-existing, must never carry avatars — **moved into R2-3/R2-6's scope 2026-11-13** rather than deferred, since it sits next to the new avatar path. See f08-accounts-profile.md.
- **Activity feed at scale**: keyset paging has no COUNT/OFFSET cost growth; no further mitigation needed at this app's size.
- **Spurious F02 `Conflict`** from Identity writes (role change, profile update) racing a capacity write on the same user row — accepted (ADR 0002), unchanged by this round.