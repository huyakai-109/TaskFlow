# F06 — Notification history (bell)
Status: Proposed 2026-11-13 · Depends on F01 D7 · Related: [f04-activity-feed.md](f04-activity-feed.md) (richer, cross-task view; this is the bell)

> **Note (2026-11-13):** a concurrent frontend fix already addressed part of this spec: `useNotificationsQuery` now fetches both read and unread rows, and `NotificationBell` shows unread as bold + a coloured dot, read as muted, with the badge counting unread only and a "Mark all as read" action. This spec's job is to make that behaviour correct and complete on the server: the badge must come from a real unread count (not a capped page), and "mark all as read" must not become one `PUT` per notification.

## Current state (verified)
Rows are never deleted: `MarkRead` sets `IsRead`/`ReadAt` (`NotificationService.cs:59-77`). The bell historically queried `isRead=false`, page 1 × 20 (`useNotificationsQuery.ts`), so read rows vanished from the bell only — this is now fixed client-side per the note above. The sidebar badge counts `result.length` (≤20) — **defect**, must switch to the real unread total (`Sidebar.tsx:149`).

## Decisions
D1 read notifications stay visible; unread are visually prominent (accent dot, bold, sorted with the rest newest-first) — **already shipped client-side**. D2 no TTL/retention in v1 (one row per send-back; volume negligible). D3 the bell popover pages its own list ("Show more"). D4 the unread count is `pagination.totalItems` of `GET /notifications?isRead=false&start=1&length=1` — no new endpoint needed for the count itself. D5 still one event type (`ReturnedForRework`); no generic event bus.

## Data (migration `AddTaskNotificationActorAndInboxIndex`, Additive)
`TaskNotifications.ActorUserId bigint NULL` FK → `Users` Restrict — the user who sent the task back; written by Move's rework path; legacy rows stay null (no backfill). Index `IX_TaskNotifications_RecipientUserId_IsRead_CreatedAt_Id`. Note: the new FK takes a shared lock on the **actor's** Users row; the rework commit bumps the **assignee's** `ConcurrencyStamp` (a different row — D5 of F01 guarantees actor ≠ assignee), so this is not a new R13-style pairing.

## API
| Verb route | Policy | Request | Success | Errors |
|---|---|---|---|---|
| GET /notifications | ViewTasks | unchanged `isRead?`, `start`, `length` ≤100 | `PaginationResultRes<List<NotificationRes>>`; `NotificationRes` **+ `task {id, title}`, + `actor {id, userName} \| null`** (additive) | Notification.GetList.Exception |
| PUT /notifications/{id}/read | ViewTasks | — | unchanged, idempotent | unchanged |
| PUT /notifications/read-all | ViewTasks | body `{ upToId: long? }` (≥1) | `ResultRes<int>` = rows marked; only the caller's unread rows with `Id ≤ upToId` (all if null), joined to non-deleted tasks; **one** `SaveChanges()`, tracked load — never one `PUT` per row | Notification.MarkAllRead.Exception |

`upToId` = the newest id the user has seen, so a notification that arrives while the popover is open is not silently marked read.

## Acceptance criteria
AC1 a read notification is still listed after marking read, with `isRead=true`. AC2 the sidebar badge equals the number of the caller's unread rows on non-deleted tasks, including beyond 20 — never `result.length` of a capped page. AC3 read-all marks only the caller's rows ≤ `upToId`; a later row stays unread. AC4 read-all twice → the second call returns 0, success. AC5 another user's rows are never touched or counted. AC6 a notification for a soft-deleted task is neither listed nor counted. AC7 a send-back records the actor; the list can show "Sent back by `<name>`". AC8 legacy rows (no `ActorUserId`) render with actor `null` and no error.

## Event code catalogue (R2-2)

`TaskNotificationEvent` (`BE/TaskFlow.Common/Enums/TaskNotificationEvent.cs`) is the one closed enum behind every `TaskNotifications.Event` row and the `event` field of `NotificationRes`. Round 2 adds no new value — D5 keeps it a single event type; this table exists so the catalogue is documented in one place rather than only in the enum's source.

| Value | Int | Written by | Recipient | `ActorUserId` |
|---|---|---|---|---|
| `ReturnedForRework` | 1 | `TaskService.Move`, on the `Review → InProgress` edge only (F01 D7) | the task's current assignee | the user who moved it back (R2-2; `null` for rows written before this migration, no backfill — AC8) |

Adding a second event value is a future decision, not assumed here: `TaskNotificationEvent` stays a plain int-backed enum (never reorder or renumber existing members — stored values would change meaning), and any new value needs its own written-by/recipient/actor row above plus the matching write path and a test, the same way `ReturnedForRework` has one. No generic event bus is introduced to support that later (D5, unchanged).

## Data retention (R2-2, explicit)

No notification row is ever deleted by this round, by `MarkRead`, or by `MarkAllRead` — all three only flip `IsRead`/`ReadAt` on existing rows. There is no TTL, archiving or purge job (D2, unchanged). Every row ever written, including legacy rows with `ActorUserId = null`, stays queryable through `GET /notifications` for as long as its task is not soft-deleted (AC6).