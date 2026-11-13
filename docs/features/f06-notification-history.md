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