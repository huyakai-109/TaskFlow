# UI completion — A/B/C classification and "Coming soon" inventory

Written for the `feature/ui-complete` task (2026-11-11), which reopened the scope cuts recorded
in `docs/design/SPEC.md` §10 points 2–6 and `docs/ROADMAP.md` R15. This is the input for any
future backend work: every **B** row names the endpoint/field it needs. Re-audited 2026-11-11
(same day, second pass) for completeness: every **A** row below now states its endpoint(s) and
field(s) explicitly, and whether the number/list it shows can be incomplete given the board's own
page-1-per-column cap (`BoardColumnPageSize` = 50, `DoneColumnPageSize` = 20 for Done,
`BE/TaskFlow.BusinessLogic/Services/TaskService.cs`) — the same constraint already named in SPEC
§10 point 9 for My tasks. Nothing here shows a possibly-wrong count as if it were certain: every
row that reads board-column data either names its "may be incomplete" caveat (same pattern as My
tasks) or, for List view, has had that gap closed this session (see A3).

Legend — **A**: real with the current API (reads data already returned by an existing endpoint).
**B**: needs a backend addition (new endpoint, field, or migration) — gets a "Coming soon"
control instead. **C**: contradicts a signed-off contract or decision — not built, ever, without a
new decision overriding the one named.

## A — real, buildable with the current API

| # | Item | Current status | Endpoint(s) + field(s) | Completeness |
|---|---|---|---|---|
| A1 | Inbox page (`/inbox`) | **Built** | `GET /notifications?isRead=&start=&length=` — `taskId`, `event`, `isRead`, `createdAt` | **Complete.** Own infinite-scroll "Show more" (`useInboxQuery`, 20/page) walks every page via `pagination.totalPages`; nothing is silently capped. |
| A2 | Team capacity page (`/team-capacity`) | **Built** | `GET /tasks/board`'s `InProgress` column (`assignee.id`, `assignee.userName`) reduced to a per-user count in `useTeamCapacity`, plus `GET /users` (`id`, `userName`, `role` — not paginated, always the full roster) | **May be incomplete.** `InProgress` is capped at `BoardColumnPageSize` (50) on page 1, raised only if the Board view's own "Show more" for that column has been clicked in the same session (shares the `['board']` cache). A `0/3`/`n/3` count is always exact *for the tasks currently loaded*; team-wide InProgress tasks beyond the first 50 (not per-user — F02 caps a person at 3 regardless) would undercount others. `TeamCapacityPage` does not yet show this caveat to the user (gap — see Summary). |
| A3 | Board List view (segmented control) | **Built** | `GET /tasks/board` (page 1) + `GET /tasks?status=&start=&length=` (page 2+, via the Board view's existing "Show more") | **Complete, as of this session's fix.** `BoardListView` originally re-rendered only the capped page-1 data with no affordance of its own — closed by giving it the same per-status "Show more" `BoardColumn` has (`onLoadMore`/`isLoadingMore` passed through from `BoardPage`), so List can always reach every row Board can. |
| A4 | Search (`⌘K`) quick-switch to a task | **Built** — client-side match over already-loaded board tasks (`title`, `id`); not a server search, there is no search endpoint | `GET /tasks/board` + any expanded "Show more" pages (same cache as A3) | **May be incomplete.** Only searches tasks already loaded into the `['board']` cache for the current session — a task in a column beyond page 1 that hasn't been expanded via "Show more" is not found. Acceptable for a "jump to a visible task" tool (not pitched as a project-wide search), but not stated in the UI itself (gap — see Summary). |
| A5 | Keyboard shortcut `C` — new task | **Built**, Board page only | n/a (UI-only, no list/count rendered) | N/A — not data-derived. |
| A6 | Keyboard shortcuts `J`/`K` — next/previous task | **Built**, Task detail page only, steps within the task's own status column | `GET /tasks/board`'s matching column (`items`, in server order) | **May be incomplete** in the same sense as A3/A4: stepping only reaches tasks already loaded for that column; unlike A3, there is no "Show more" reachable from Task detail itself. In practice, F02 keeps any one person's `InProgress` count ≤ 3 (always within page 1), and a single status column exceeding 50/20 items is an edge case, not the common one — still a real, documented gap, not silently "impossible." |
| A7 | Keyboard shortcut `⌘↵`/`Ctrl+Enter` — primary action | **Built**, Task detail (first allowed Move) and New task dialog (submit) | n/a (acts on the one task already loaded on the page) | N/A — not data-derived. |
| A8 | Assign picker "HAS ROOM · MOST FIRST" / "AT CAPACITY" grouping + disabled "Full" rows (§5.5) | **Not built this session** — unblocked | Same as A2 | Same caveat as A2 — inherits it, not yet built. |
| A9 | Board toolbar AvatarStack + capacity pill + Team popover (§5.1, §5.2) | **Not built this session** — unblocked; only the standalone Team capacity page (A2) exists, not the popover-under-avatar-stack variant | Same as A2 | Same caveat as A2 — inherits it, not yet built. |
| A10 | Overdue list — "Due today, not overdue yet" group (§5.7) | **Not built this session** — unblocked | `GET /tasks?isOverdue=false&start=&length=` filtered client-side to `dueDate` = today, **not** derived from the board cache | **Complete, if built this way.** Unlike A2/A8/A9, this should be its own paginated fetch (same shape as the existing "Overdue now" `useOverdueTasksQuery`), not a read of the capped board cache — flagged here so a future slice doesn't accidentally wire it to `useBoardQuery` and inherit a cap it doesn't need to have. |
| A11 | My tasks — "Sent back by `<name>` for rework" chip (§5.8) | **Not built this session** — unblocked | `GET /tasks/{id}/history` — latest `Review → InProgress` entry's `actorName` | **Complete.** Reads one task's full history (not paginated-away in practice; `length=100` default already covers any real task), for a small, already-bounded set of cards (≤3 In-progress + Up next + Review) — not a board-wide read, no cap applies. |
| A12 | Done card "Approved by `<name>` · `<date>`" (§4 TaskCard) | **Not built this session** — unblocked | `GET /tasks/{id}/history`, same read as A11, once per visible Done card | **May be incomplete** only in the sense that the Done *column itself* is capped at `DoneColumnPageSize` (20) before any of these cards are fetched — a pre-existing Board/List cap (A3), not a new one this feature would add. |

**Two caveat-display gaps found by this audit, not yet fixed:** A2/A8/A9's "may be incomplete"
condition is not shown anywhere in `TeamCapacityPage` itself (no footnote), and A4's search has no
in-UI note that it only reaches loaded tasks. Both are copy-only additions for a future slice —
tracked here rather than silently left for someone to rediscover.

## B — needs a backend addition (real "Coming soon" stubs)

| # | Item | Behaviour now | Endpoint/field needed | Read or write | Migration? |
|---|---|---|---|---|---|
| B1 | Overdue list — "Flag cleared this week" group (§5.7), "Was N days late" / "Cleared — moved to Done …" / "Cleared — due date moved …" copy | Nav/section not shown; no stub control exists to replace (there's no single button to wire to "Coming soon" — the whole group is just absent) | An overdue-cleared history event (or equivalent) carrying: the date it was flagged, the date/reason it was cleared, and whether clearing was "finished" vs. "due date moved" | Read | No (a new `TaskHistoryEvent` value + write-path entry, not a schema rewrite) — but is backend application work, not just a read |
| B2 | My tasks — "You finished N tasks this week" footer line | Footer line not shown | A per-user, dated aggregate: count of `StatusChanged → Done` events for the caller in the last 7 days | Read | No (aggregate query), but needs new service/endpoint surface, e.g. `GET /tasks/me/finished-this-week` or similar — not designed here, left to the backend lead |
| B3 | Overdue list — "Last check: today, 00:05" | Not shown | Either expose the scheduler's last successful run timestamp, or derive it is accepted as "tonight at 00:05" copy without a real value — needs a decision, not just a read | Read | Possibly none (could be a config/health value) |
| B4 | Team capacity (A2) / Assign picker grouping (A8) — the `3` in every `n/3`/"Full" label | **Built anyway, with a flagged assumption**: `useTeamCapacity.ts`'s `IN_PROGRESS_LIMIT` is hardcoded to `3`, matching the backend's *default* `Workflow:MaxInProgressPerAssignee` (`BE/TaskFlow.Api/appsettings.json`) — but that value is configurable (`ConfigKeys.Workflow.MaxInProgressPerAssignee`) and no endpoint returns it to the client. If an environment ever configures a value other than 3, this display goes stale silently (coding-standards.md §4: "limits agree across layers" — this one doesn't, for lack of a layer to read it from) | A read-only field or endpoint exposing the configured `MaxInProgressPerAssignee` (e.g. a field on `GET /users` or `GET /tasks/board`, or a tiny `GET /config/workflow` — not designed here) | Read | No |

No "Coming soon" control was built for B1–B3 this session (B4 was built anyway, as a stated
assumption rather than a stub — it's a single integer with a safe, documented default, not a
missing feature): none of them replace an existing,
clickable control in the current app (they are missing *sections*, not disabled *buttons*) — per
the brief's own rule ("if the empty frame is meaningless, a nav item showing Coming soon is
enough"), and no nav item points at any of these specifically. If a future slice adds the
containing section's frame before the data exists, wire its action to `ComingSoon`
(`FE/src/shared/components/ComingSoon.tsx` / `useComingSoon.ts`, built this session) the same way
`TeamCapacityPage`'s "not yet wired" fallback would, rather than inventing numbers.

## C — contradicts a signed-off contract/decision, not built

| # | Item | Why not |
|---|---|---|
| C1 | Reviewer field anywhere (board card `TF-9 · reviewer Minh`, Task detail properties panel, New task modal) | F01 D5 — no reviewer role; anyone but the current assignee may move a task out of Review. Adding a Reviewer field would contradict the signed-off workflow contract. `SPEC.md` §10 point 1, unchanged. |
| C2 | Assign picker reassignment flow / `Task.Move.CapacityReached` opening an inline picker (§5.6 dialog 2, "Lan is at capacity") | `docs/features/f02-capacity.md` AC22 requires a plain refusal + rollback for this exact code; building the picker for it was tried once (slice 4), found to violate AC22, and reverted 2026-11-09. `SPEC.md` §10 point 8, unchanged. |

## Summary

- **A: 12** items identified; **7 built** this session (A1–A7: Inbox, Team capacity, List view,
  search, and the three keyboard shortcuts); **5 not built**, all unblocked for a future slice
  without any backend change (A8–A12).
- **Completeness re-audit (2026-11-11, second pass):** every A item above now states its
  endpoint(s)/field(s) and whether its number/list can be incomplete given the board's
  page-1-per-column cap. One real gap found and fixed this session: List view (A3) had no
  "Show more" of its own and could silently truncate past the Board view's own cap — closed.
  Two gaps found and left open (copy-only, tracked above, not yet fixed): Team capacity /
  Assign-picker-grouping's "may be incomplete" condition (A2/A8/A9) and search's
  loaded-tasks-only scope (A4) are not disclosed anywhere in the UI itself.
- **B: 4** items, genuinely blocked on backend work (one of them — B4 — built anyway as a stated
  assumption, not a stub, since it's a safe-defaulted integer rather than a missing feature).
  Grouped by what they need: (1) an overdue-cleared history event (B1), (2) a dated per-user
  aggregate endpoint (B2), (3) a scheduler-status value or a decision to invent none (B3), (4) a
  way to read the configured in-progress limit instead of assuming the default of 3 (B4).
- **C: 2** items, not built because they contradict a signed-off contract (F01 D5, F02 AC22) —
  never build these without a new decision overriding the one named.
