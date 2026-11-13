# F04 — Activity feed (cross-task history)

| | |
|---|---|
| **Source** | User decision 2026-11-13 (sidebar "Inbox" replacement brainstorm). Not a BRS section; extends [F04](f04-audit.md) on the read side only. |
| **Status** | Planned · Spec owner: user · next: architect review |
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
- A "new since last visit" badge or any per-user seen state (Q6).
- Real-time push; periodic auto-refresh of the list (R9).
- Filter by task, keyword search, export, retention/archiving, digest or notification of feed events.
- Recording anything F04 does not already record (title/description edits stay unrecorded).
- Changing the notification bell (see Q-A) and the "Unassigned" Board filter (later, separate work).
- Any new permission or role restriction.

## Rules and behaviour
R1 Visibility: any authenticated caller holding `ViewTasks` sees every entry, whatever the task's assignee or status — one shared board (F01 D8, F04 D5). No new permission.
R2 Order: newest first by the entry's moment, ties broken by entry id descending — deterministic across reads (mirror of F04 AC18).
R3 Each item carries what one row needs to be read alone: the F04 structured fields and rendered sentence (F04 D1, AC21), the moment in UTC and in the configured zone (F04 D4), the actor's display name or "System" (F04 AC8), the task's id and title, and whether the task is currently deleted.
R4 Entries of soft-deleted tasks are included (F04 D6); the task is shown as deleted and is not linked.
R5 The task title shown is the task's **current** title (titles are not historised — F04 out of scope).
R6 An entry whose actor user has since been deleted still shows that actor's name (as F04 T3 already does per task).
R7 Filters combine with AND. Absent filter = no restriction. Event type accepts one or more of the 8 F04 values. Actor accepts one user or "System". Date range is inclusive, `from` ≤ `to`, each a calendar date interpreted in the configured zone (`Asia/Ho_Chi_Minh`); either bound may be omitted.
R8 Paging is stable while new entries are written: fetching the next page never repeats an entry already returned and never skips an older one, even if new entries were recorded in between. (Mechanism: see *Data / endpoint intent*.)
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
AC7 Given more entries than one page, when the client asks for the next page, then it receives the next-older entries and a signal of whether more exist; the last page says no more exist.
AC8 Given page 1 was read and 3 new entries were then recorded, when page 2 is read, then page 2 contains no entry from page 1 and no entry older than page 1's last entry is missing.
AC9 Given a requested page size above the agreed maximum or below 1, then the request is refused with a validation error in the standard envelope (bounds and code: architect).

Filters
AC10 Given actor = member1, when the feed is read, then only entries whose actor is member1 are returned.
AC11 Given actor = System, then only null-actor entries are returned.
AC12 Given event types = {StatusChanged, Reopened}, then only those two event types are returned.
AC13 Given from = to = 2026-11-12, then exactly the entries whose moment falls on 12 Nov 2026 in Asia/Ho_Chi_Minh (17:00 UTC 11 Nov ≤ t < 17:00 UTC 12 Nov) are returned — an entry at 23:30 local on 12 Nov is included, one at 00:10 local on 13 Nov is not.
AC14 Given from later than to, then the request is refused with its own error code (name per `<Feature>ControllerMS` convention, set by architect) and no entries.
AC15 Given an unknown event type value or a malformed date, then a validation error is returned in the standard envelope.
AC16 Given an actor id that matches no user, then the result is an empty list, not an error. (ASSUMPTION — cheap to change to a validation error.)
AC17 Given filters combined (actor + type + dates), then only entries matching all of them are returned.

Empty / zero
AC18 Given no entry matches the filters, then the response is success with an empty list and total/has-more indicating zero; the client shows "no activity matches these filters" with a way to clear filters.
AC19 Given filters are empty and the feed is empty (fresh database), then the client shows a distinct "no activity yet" state. (In practice any created task produces an entry — F04 D3.)

Permission
AC20 Given an unauthenticated request, then 401 and no entries.
AC21 Given an authenticated caller without `ViewTasks`, then 403 and no entries — the feed must not become a side channel (F04 AC16).

Effect on existing data
AC22 Given any feed read, when the history table and task table are inspected, then no row was inserted, updated or deleted.
AC23 Given the feature is deployed, then the existing per-task history endpoint, its order (oldest first) and its response shape are unchanged.

Sidebar
AC24 Given a signed-in user, when the sidebar renders (expanded, collapsed, mobile drawer), then "Activity" appears in the slot "Inbox" occupied, with no counter, and "Inbox" no longer appears.
AC25 Given the Activity page, then it handles loading, error (message from the error code), empty (AC18/AC19) and success states at 390 / 768 / 1440 px.

## Data / endpoint intent (for the architect — not a design)
- One new **read** endpoint returning F04 entries across all tasks, in the standard paged envelope, policy `ViewTasks`. Route, names and field list: architect.
- Each entry needs data the per-task endpoint does not carry: task title and task-deleted flag (R3). Must come from one query (join/projection), not one call per row — the client-side N+1 precedent (COMING-SOON B5/B6) is forbidden.
- **Paging — recommend cursor (keyset) on (moment, id) descending.** Why: (1) the feed is newest-first and grows while being read, so offset paging shifts rows between requests and produces duplicates or skips — R8/AC8 cannot hold with plain offset; (2) the table grows faster than any other (F04 tech notes), and keyset cost does not grow with page depth. Trade-off: every existing list uses offset `start`/`length` (F01 T5, F03 D5 "no new paging style"), so this is a **second paging pattern** — a decision for the architect (coding-standards §1 "one pattern per concern"). Precedent-consistent alternative: keep `start`/`length` but pin the first page's newest id as an upper bound passed on every later page; it also satisfies AC8, at the cost of offset's deep-page scan.
- Index: the existing `(TaskId, CreatedAt, Id)` does not serve a cross-task, time-ordered read or an actor filter; a new index (additive migration) is likely. Architect decides.
- Date filter: convert local calendar dates to UTC bounds once, compare the raw column (sargable — backend.md §6).
- Reads must bypass the task soft-delete filter for title/deleted flag and the user filter for actor names (as F04 T3 does); EF Core 9 `IgnoreQueryFilters()` drops every filter on the entity.
- Rendered sentence: reuse F04's read-time templates (D1); no stored prose.

## UI
- Page "Activity": rows per SPEC §4 *Activity row* (avatar 20, actor bold, sentence, timestamp right `15 Oct, 09:30`; System = danger-soft clock + "System"); task reference as link, or plain + "deleted" marker.
- Filter bar: actor picker (users from `GET /users` + "System"), date from/to, event type multi-select; "Refresh" button; "Show more"/load-next.
- States: loading, error, empty-no-data, empty-filtered, success. No permission-denied UI beyond the envelope error (every seeded role has `ViewTasks` — ASSUMPTION).
- Sidebar: replace Inbox item; Topbar breadcrumb "Activity".

## Operations
None expected (no setting, job, secret). One additive migration likely (index) — needs approval to apply (workflow §3).

## Follow-up doc changes (on approval)
- `f04-audit.md` Out of scope: strike "a cross-task activity feed", add "lifted 2026-11-13 → f04-activity-feed.md".
- `docs/ROADMAP.md`: new unit row (F04 extension, two slices: BE endpoint + tests; FE page + sidebar swap).
- `CLAUDE.md` In scope: propose a diff adding "cross-task activity feed (read-only, F04 extension, 2026-11-13)" and replacing "Inbox" — a proposal, not a silent edit.
- `docs/design/SPEC.md` §10: new numbered point recording the Inbox → Activity swap.

## Open questions
- QUESTION Q-A — **resolved 2026-11-13, independently of this spec's approval.** Inbox was removed (its own FE-only task; `docs/design/SPEC.md` §3/§10 point 5) before this feed was built or scheduled. At the same time, the bell's own disappearing-on-read behaviour was found to be a client-side filter bug, not an API limitation (`GET /notifications` already returns both read and unread when `isRead` is omitted) — `useNotificationsQuery` now fetches both, `NotificationBell` shows unread as bold + a coloured dot and read as muted, counts the badge from unread only, and adds "Mark all as read". So read notifications are reachable again through the bell itself; this feed, if approved, would still be a separate, richer, cross-task view, not the only way back to a read notification.
- QUESTION Q-B: once the nav item is gone, does `/inbox` stay reachable (future bell "See all" target), redirect to `/activity`, or get removed?
- ASSUMPTION: no interval polling (R9); Activity takes Inbox's first position (Q8); unknown actor id → empty list (AC16); a deleted task cannot be opened from the feed (AC4); every seeded role holds `ViewTasks`.
- ASSUMPTION: the feed shows the current task title, not the title at the time of the entry (R5).
