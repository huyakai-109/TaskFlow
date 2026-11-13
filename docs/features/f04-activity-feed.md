# F04 — Activity feed (cross-task history)

| | |
|---|---|
| **Source** | User decision 2026-11-13 (sidebar "Inbox" replacement brainstorm). Not a BRS section; extends [F04](f04-audit.md) on the read side only. |
| **Status** | **Approved** 2026-11-13 — technical contract below reviewed by architect; paging decision is [ADR 0007](../adr/0007-keyset-paging-for-activity-feed.md) (keyset, superseding an earlier offset+anchor draft). Awaiting the user's sign-off on the open items listed at the end. |
| **Overrides** | `f04-audit.md` → *Scope* → Out of scope: "a cross-task activity feed" — **lifted 2026-11-13 by the user**, for a read-only feed only. Every other F04 decision (D1–D7) and out-of-scope item (title/description edits, diffing, export, retention) is unchanged. |
| **Depends on** | F04 trail (shipped, slices 1.1–1.4) · F05 `OverdueMarked` entries · F01 D8 / F04 D5 visibility |

## Goal
Anyone signed in can see, newest first, every recorded change across the whole board — who did what to which task and when — from one sidebar entry that replaces "Inbox".

## Scope
In:
- One read-only, paged list of F04 history entries across all tasks, newest first, including soft-deleted tasks and System entries.
- Filters: actor (a user, or System) · date range (calendar days in the configured zone) · event type (one or more of the 8 F04 events).
- Sidebar: the "Inbox" nav item is replaced by "Activity"; breadcrumb "Activity".

Out (what someone might assume is included):
- Any write: no comments, reactions, mark-as-seen, pin, delete or edit of entries (F04 D2 unchanged).
- A "new since last visit" badge or any per-user seen state.
- Real-time push; periodic auto-refresh of the list (R9 — reload on open, on refocus, and via an explicit "Refresh" button only).
- Filter by task, keyword search, export, retention/archiving, digest or notification of feed events.
- Recording anything F04 does not already record (title/description edits stay unrecorded).
- Changing the notification bell (see [f06-notification-history.md](f06-notification-history.md)) and the "Unassigned" Board filter (later, separate work).
- Any new permission or role restriction.

## Rules and behaviour
R1 Visibility: any authenticated caller holding `ViewTasks` sees every entry, whatever the task's assignee or status — one shared board (F01 D8, F04 D5). No new permission. **Verified:** all three seeded roles hold `ViewTasks` (`RolePolicies.cs`).
R2 Order: newest first by the entry's moment, ties broken by entry id descending — deterministic across reads (mirror of F04 AC18).
R3 Each item carries what one row needs to be read alone: the F04 structured fields and rendered sentence (F04 D1, AC21), the moment in UTC and in the configured zone (F04 D4), the actor's name or "System" (F04 AC8), the task's id and title, and whether the task is currently deleted.
R4 Entries of soft-deleted tasks are included (F04 D6); the task is shown as deleted and is not linked.
R5 The task title shown is the task's **current** title (titles are not historised — F04 out of scope).
R6 An entry whose actor user has since been deleted still shows that actor's name (as F04 T3 already does per task).
R7 Filters combine with AND. Absent filter = no restriction. Event type accepts one or more of the 8 F04 values. Actor accepts one user or "System". Date range is inclusive, `from ≤ to`, each a calendar date interpreted in the configured zone (`Asia/Ho_Chi_Minh`); either bound may be omitted.
R8 Paging is stable while new entries are written: fetching the next page never repeats an entry already returned and never skips an older one, even if new entries were recorded in between. (Mechanism: keyset paging, below.)
R9 Freshness: the list loads on page open, reloads on window refocus and on an explicit "Refresh" action; no interval polling.
R10 Nothing in the feed changes data: the feature adds no write path and no column that any write path must maintain.

## Acceptance criteria
Happy path
AC1 Given entries exist on several tasks, when a Member opens Activity, then entries from all those tasks are returned newest first, each with actor name, event, rendered sentence, local time, task id and current title.
AC2 Given two entries with the same moment, when the first page is read twice, then both responses return them in the same order (higher id first).
AC3 Given the F05 job flagged a task, when the feed is read, then that entry's actor is "System" (null actor id), never a user and never id 0.
AC4 Given a Manager deleted task #7, when the feed is read, then #7's earlier entries and its `Deleted` entry all appear, marked as a deleted task, with no link to open it.
AC5 Given an entry whose actor user was later deleted, when the feed is read, then the actor's name still appears.
AC6 Given a `Reopened` entry, when it is read, then the reason text appears verbatim (F04 AC6).

Paging
AC7 Given more entries than one page, when the client asks for the next page, then it receives the next-older entries and a `hasMore` signal; the last page says `hasMore: false`.
AC8 Given page 1 was read and 3 new entries were then recorded, when page 2 is read, then page 2 contains no entry from page 1 and no entry older than page 1's last entry is missing.
AC9 Given a requested page size above 100 or below 1, then the request is refused with a validation error (`Request.Validation.Failed`, `validates[]`); default page size is 30.

Filters
AC10 Given actor = member1, when the feed is read, then only entries whose actor is member1 are returned.
AC11 Given actor = System, then only null-actor entries are returned.
AC12 Given event types = {StatusChanged, Reopened}, then only those two event types are returned.
AC13 Given from = to = 2026-11-12, then exactly the entries whose moment falls on 12 Nov 2026 in Asia/Ho_Chi_Minh (17:00 UTC 11 Nov ≤ t < 17:00 UTC 12 Nov) are returned — an entry at 23:30 local on 12 Nov is included, one at 00:10 local on 13 Nov is not.
AC14 Given `from` later than `to`, then the request is refused with `Activity.GetList.InvalidDateRange` and no entries.
AC15 Given an unknown event type value or a malformed date, then a validation error is returned in the standard envelope.
AC16 Given an actor id that matches no user, then the result is an empty list, not an error. (ASSUMPTION — cheap to change to a validation error.)
AC17 Given filters combined (actor + type + dates), then only entries matching all of them are returned.

Empty / zero
AC18 Given no entry matches the filters, then the response is success with an empty list and `hasMore: false`; the client shows "no activity matches these filters" with a way to clear filters.
AC19 Given filters are empty and the feed is empty (fresh database), then the client shows a distinct "no activity yet" state. (In practice any created task produces an entry — F04 D3.)

Permission
AC20 Given an unauthenticated request, then 401 and no entries.
AC21 Given an authenticated caller without `ViewTasks`, then 403 and no entries — the feed must not become a side channel (F04 AC16).

Effect on existing data
AC22 Given any feed read, when the history table and task table are inspected, then no row was inserted, updated or deleted.
AC23 Given the feature is deployed, then the existing per-task history endpoint, its order (oldest first) and its response shape are unchanged.

Sidebar
AC24 Given a signed-in user, when the sidebar renders (expanded, collapsed, mobile drawer), then "Activity" appears in the slot "Inbox" occupied, with no counter, and "Inbox" no longer appears, and `/inbox` is **removed entirely** (not redirected).
AC25 Given the Activity page, then it handles loading, error (message from the error code), empty (AC18/AC19) and success states at 390 / 768 / 1440 px.

---

## Technical contract (architect, 2026-11-13)

### Paging: cursor (keyset), not offset — [ADR 0007](../adr/0007-keyset-paging-for-activity-feed.md)
Plain offset duplicates/skips rows under concurrent writes to an append-only, newest-first table (fails R8/AC8). Pinning "page 1's newest id" as an upper bound does not fix it either: `CreatedAt` is stamped at `SaveChanges`, `Id` at INSERT, so two concurrent commits can give a later id to an older timestamp, and a late-committing transaction can still shift the offsets under the pin. Keyset has no such failure mode and its cost does not grow with page depth. This is a **second paging style** next to the codebase's offset `start`/`length` (F01 T5, F03 D5) — recorded as a decision, not a silent deviation: **offset stays the default for bounded, mutable lists; keyset only for append-only streams read newest first.**

### Endpoint
`GET api/v1/activity` — new `ActivityController` (`BaseController`), calling a new `GetFeed` on the existing `ITaskHistoryService` so sentence rendering stays in one place (`TaskHistoryMessages.Build`). Policy `ViewTasks`. The per-task history endpoint is untouched (AC23).

**Request** `ActivityGetListReq` `[FromQuery]`:

| Field | Type / bounds | Notes |
|---|---|---|
| `cursor` | string?, max 100 | Opaque base64url of the last row's `(CreatedAt, Id)`. Absent = first page. Undecodable → `Activity.GetList.InvalidCursor`. The client resets it whenever filters change. |
| `length` | int, `[Range(1,100)]`, default 30 | AC9. |
| `actor` | string?, regex `^(system\|[1-9][0-9]{0,18})$` | A user id, or `system` meaning `ActorUserId IS NULL` (AC11). An unknown id → empty list (AC16). |
| `from`, `to` | `DateOnly?` `yyyy-MM-dd`, inclusive | Converted once to UTC in the configured zone: `[from 00:00 local, (to+1) 00:00 local)`. `from > to` → `Activity.GetList.InvalidDateRange` (AC14). |
| `events` | `TaskHistoryEvent[]?` (repeat `events=`), ≤8 | Each checked with `Enum.IsDefined` (the query binder accepts out-of-range numbers; `[EnumDataType]` does not validate array elements) → `Request.Validation.Failed` on an unknown value (AC15). |

Filters combine with AND (R7, AC17). Order: `CreatedAt DESC, Id DESC` (R2, AC2).

**Response** `CursorResultRes<List<ActivityRes>>` — `ResultRes<T>` plus a `page` object (new envelope, ADR 0007):

```jsonc
{ "success": true, "errorCode": null, "error": null, "validates": [],
  "result": [{
    "id": 912, "event": "StatusChanged",
    "task": { "id": 7, "title": "Fix the login redirect", "isDeleted": false },
    "actorUserId": 12, "actorName": "member1",            // both null = System (AC3)
    "fromValue": "Review", "toValue": "InProgress", "reason": null,
    "occurredAt": "2026-11-12T16:30:00.000000+00:00",
    "occurredAtLocal": "2026-11-12T23:30:00.000000+07:00",
    "message": "member1 moved the task from Review to In Progress" }],
  "page": { "nextCursor": "MjAyNi0…", "hasMore": true, "pageSize": 30 } }
```

`actorDisplayName`/`actorAvatarUrl` are added later, additively, once F08 lands. No count field (AC7/AC18 use `hasMore`, not a total).

**Error codes** (new `BE/TaskFlow.Common/Constants/Messages/ActivityControllerMS.cs`): `Activity.GetList.InvalidDateRange` (400) · `Activity.GetList.InvalidCursor` (400) · `Activity.GetList.Exception` (500). 401/403 from the framework (AC20, AC21).

**Query — 2 round trips, no N+1:**
1. Page query reading `length+1` rows (to compute `hasMore`): `TaskHistories` with filters + keyset condition → inner join `Tasks` with `IgnoreQueryFilters()` (title, `IsDeleted`; R3, R4, AC4) → left join `Users` with `IgnoreQueryFilters()` on `ActorUserId` (R6, AC5) → projection only.
2. One batched lookup of user names referenced by `AssigneeChanged` values (existing `TaskHistoryService` pattern).

No-tracking; writes nothing (AC22). `IgnoreQueryFilters()` drops only `IsDeleted` on both entities — the deliberate, documented use (`CLAUDE.md`).

**Indexes** (migration `AddTaskHistoriesFeedIndexes`, **Additive** — same migration named in [f04-audit.md](f04-audit.md) R2 and [round-2-impact.md](round-2-impact.md)):
- `IX_TaskHistories_CreatedAt_Id (CreatedAt, Id)` — unfiltered feed, the keyset condition, the date range.
- `IX_TaskHistories_ActorUserId_CreatedAt_Id` — actor filter, including `system` (InnoDB indexes NULLs).
- No index for the event filter (8 values, checked inside the time range).
- `IX_TaskHistories_ActorUserId` (EF-generated) becomes redundant; left in place — dropping it would not be additive.
- Record `EXPLAIN` for: unfiltered page 1, a deep cursor page, actor + date range, events + date range.

### Spec wording applied (vs. the original draft)
AC7/AC18 use `hasMore`, no total. AC9 bounds 1..100, default 30, code `Request.Validation.Failed`. AC14 code `Activity.GetList.InvalidDateRange`. Q-B (was `/inbox` reachable after Inbox is gone?) is **closed: removed entirely**, no redirect — the router has no catch-all route (`FE/src/app/router.tsx`); add a `*` not-found route in the same frontend slice.

## UI
- Page "Activity": rows per SPEC §4 *Activity row* (avatar 20, actor bold, sentence, timestamp right "15 Oct, 09:30"; System = danger-soft clock + "System"); task reference as a link, or plain text + "deleted" marker.
- Filter bar: actor picker (`GET /users` + "System"), date from/to, event type multi-select; "Refresh" button; "Show more"/load-next using the cursor.
- States: loading, error, empty-no-data, empty-filtered, success. No permission-denied UI beyond the envelope error (every seeded role holds `ViewTasks`).
- Sidebar: replaces the Inbox item; topbar breadcrumb "Activity".

## Operations
No new setting, job or secret. One additive migration (two indexes) — needs the user's approval to apply (`workflow.md` §3).

## Follow-up doc changes (on approval)
- [f04-audit.md](f04-audit.md) Out of scope: already struck above (R2 section).
- `docs/ROADMAP.md`: new unit row (F04 extension) — slice A1 backend (endpoint, `CursorResultRes` envelope, migration, tests), slice A2 frontend (page, filters, cursor `useInfiniteQuery`, sidebar swap, `/inbox` removal, `*` route).
- `CLAUDE.md` (proposed diff, not a silent edit): *In scope* — add "cross-task activity feed (read-only, F04 extension, 2026-11-13)"; client list "history view" → "history view and Activity page"; *How this codebase works* — add a **Paging** bullet: "offset `start`/`length` + `PaginationRes` is the default; keyset with `CursorResultRes` only for append-only newest-first streams (ADR 0007)".
- [COMING-SOON.md](../design/COMING-SOON.md): A1 Inbox → Removed (replaced by [f06](f06-notification-history.md) bell history + this feed).
- [SPEC.md](../design/SPEC.md) §10: Inbox → Activity in the same nav slot, no counter, `/inbox` removed; bell keeps read history (unread bold + dot, read muted, badge counts unread only, "Mark all as read").

## Open questions
- QUESTION Q-A — **resolved 2026-11-13, independently of this spec's approval.** Inbox was removed (its own FE-only task) before this feed was built or scheduled; the bell's disappearing-on-read behaviour was a client-side filter bug, not an API limitation (`GET /notifications` already returns both read and unread when `isRead` is omitted) — fixed client-side (see [f06-notification-history.md](f06-notification-history.md)). This feed, once approved, is still a separate, richer, cross-task view, not the only way back to a read notification.
- QUESTION Q-B — **resolved**: `/inbox` is removed entirely, no redirect (see "Spec wording applied" above).
- ASSUMPTION: Activity takes Inbox's first position in the nav; unknown actor id → empty list (AC16); a deleted task cannot be opened from the feed (AC4); every seeded role holds `ViewTasks` (verified).
- ASSUMPTION: the feed shows the current task title, not the title at the time of the entry (R5).
- Needs sign-off: ADR 0007 (keyset paging), the `actor=system` encoding, AC16's empty-list behaviour, no total count (`hasMore` only), the `*` not-found route, applying migration `AddTaskHistoriesFeedIndexes`, and the `CLAUDE.md` diff.