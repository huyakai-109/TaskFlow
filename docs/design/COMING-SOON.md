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
tasks) or, for List view, has had that gap closed this session (see A3). **Third pass, same day:**
the two remaining caveat-display gaps (A2/A8/A9, A4) were closed with real UI, not just documented;
A11/A12 turned out to be N+1 fetch patterns on closer review and were reverted to category B
(B5/B6); real "Coming soon" stubs (`—` placeholders, no API call, confirmed via Playwright) were
built for the four genuinely-blocked B items (B1–B4).

Legend — **A**: real with the current API (reads data already returned by an existing endpoint).
**B**: needs a backend addition (new endpoint, field, or migration) — gets a "Coming soon"
control instead. **C**: contradicts a signed-off contract or decision — not built, ever, without a
new decision overriding the one named.

## A — real, buildable with the current API

| # | Item | Current status | Endpoint(s) + field(s) | Completeness |
|---|---|---|---|---|
| A1 | Inbox page (`/inbox`) | **Removed 2026-11-13** | `GET /notifications?isRead=&start=&length=` — `taskId`, `event`, `isRead`, `createdAt` | N/A — the nav item, route and `FE/src/features/inbox/` were removed; a replacement for the sidebar slot is undecided (`docs/design/sidebar-inbox-replacement.md`). The topbar notification bell is unrelated and unaffected. |
| A2 | Team capacity page (`/team-capacity`) | **Built** | `GET /tasks/board`'s `InProgress` column (`assignee.id`, `assignee.userName`) reduced to a per-user count in `useTeamCapacity`, plus `GET /users` (`id`, `userName`, `role` — not paginated, always the full roster) | **May be incomplete, now disclosed.** `InProgress` is capped at `BoardColumnPageSize` (50) on page 1, raised only if the Board view's own "Show more" for that column has been clicked in the same session (shares the `['board']` cache). `TeamCapacityPage.tsx` and `TeamCapacityPopover.tsx` both show "This list may not be complete — see the full In Progress column on the Board" whenever `useTeamCapacity().mayBeIncomplete` is true. |
| A3 | Board List view (segmented control) | **Built** | `GET /tasks/board` (page 1) + `GET /tasks?status=&start=&length=` (page 2+, via the Board view's existing "Show more") | **Complete, as of this session's fix.** `BoardListView` originally re-rendered only the capped page-1 data with no affordance of its own — closed by giving it the same per-status "Show more" `BoardColumn` has (`onLoadMore`/`isLoadingMore` passed through from `BoardPage`), so List can always reach every row Board can. |
| A4 | Search (`⌘K`) quick-switch to a task | **Built** — client-side match over already-loaded board tasks (`title`, `id`); not a server search, there is no search endpoint | `GET /tasks/board` + any expanded "Show more" pages (same cache as A3) | **May be incomplete, now disclosed.** Only searches tasks already loaded into the `['board']` cache for the current session. `BoardSearch.tsx`'s results dropdown shows "May not be complete — see the full Board" whenever any column has unloaded pages (a `mayBeIncomplete` prop computed in `BoardPage.tsx`). |
| A5 | Keyboard shortcut `C` — new task | **Built**, Board page only | n/a (UI-only, no list/count rendered) | N/A — not data-derived. |
| A6 | Keyboard shortcuts `J`/`K` — next/previous task | **Built**, Task detail page only, steps within the task's own status column | `GET /tasks/board`'s matching column (`items`, in server order) | **May be incomplete** in the same sense as A3/A4: stepping only reaches tasks already loaded for that column; unlike A3, there is no "Show more" reachable from Task detail itself. In practice, F02 keeps any one person's `InProgress` count ≤ 3 (always within page 1), and a single status column exceeding 50/20 items is an edge case, not the common one — still a real, documented gap, not silently "impossible." |
| A7 | Keyboard shortcut `⌘↵`/`Ctrl+Enter` — primary action | **Built**, Task detail (first allowed Move) and New task dialog (submit) | n/a (acts on the one task already loaded on the page) | N/A — not data-derived. |
| A8 | Assign picker "HAS ROOM · MOST FIRST" / "AT CAPACITY" grouping + disabled "Full" rows (§5.5) | **Built** — in both pickers that assign while a task occupies/will occupy an `InProgress` slot: `AssignAndMoveDialog` (`Task.Move.AssigneeRequired`, always about to start) unconditionally, and `AssigneePickerDialog` (Task detail's general reassignment) **only when the task's current status is `InProgress`** (F02 D2 `TaskCapacityRule.OccupiesSlot` — reassigning a Backlog/Todo/Review/Done task has no capacity limit per F01 D1, so grouping/disabling there would be actively misleading; that picker stays its old plain alphabetical list for those four statuses) | Same as A2 | Same caveat as A2 — inherits it. |
| A9 | Board toolbar AvatarStack + capacity pill + Team popover (§5.1, §5.2) | **Built 2026-11-11, removed 2026-11-13** | Same as A2 | Removed: capacity already has its own page (Team capacity), so repeating "`N` at capacity" + an avatar stack/popover here was redundant, and the avatar-ring styling read as live presence it never was. Replaced by `ViewingNowPlaceholder` (`FE/src/features/board/components/ViewingNowPlaceholder.tsx`) — the signed-in viewer's own avatar only, reserving the same toolbar slot for a real presence feature later; no fake "others online" list, since there is no live-presence source. `AvatarStack.tsx`/`TeamCapacityPopover.tsx` deleted as dead code. |
| A10 | Overdue list — "Due today, not overdue yet" group (§5.7) | **Built** — `useDueTodayTasksQuery`, its own paginated fetch (not derived from the board cache, per this row's own earlier flag) | `GET /tasks?isOverdue=false&start=&length=` filtered client-side to `dueDate` = today | **Honestly bounded, same pattern as "Overdue now".** Only as many non-overdue tasks as have been fetched are ever scanned; its own "Check more non-overdue tasks for 'due today'" button (distinct copy from a per-row "Show more") fetches further pages and says exactly how many of the total non-overdue count have been checked so far — never claims completeness it doesn't have. |

**A11 and A12 were built, then reverted and moved to category B** on closer review (third pass,
2026-11-11) — see B5/B6 below. Both had an N+1 fetch pattern this project's own `backend.md` §6
rule ("no N+1: join or project in one query") exists precisely to catch, just on the client side
instead: one extra `GET /tasks/{id}/history` request per *rendered row/card*, not a single batched
call. Bounded (≤3 rows / ≤20 Done cards) is not the same as "not N+1" — removed rather than kept
as a quietly-scaling pattern.

**Two caveat-display gaps found by the 2026-11-11 completeness audit — now fixed (third pass):**
A2/A8/A9's "may be incomplete" condition is now shown in `TeamCapacityPage.tsx` and
`TeamCapacityPopover.tsx` (a `useTeamCapacity().mayBeIncomplete` flag, linking to the full Board),
and A4's search now shows the same note inside its results dropdown (`BoardSearch.tsx`, a
`mayBeIncomplete` prop computed in `BoardPage.tsx` from every column's own pagination).

## B — needs a backend addition (real "Coming soon" stubs)

| # | Item | Stub lives in (file / control) | Endpoint/field needed | Read or write | Migration? |
|---|---|---|---|---|---|
| B1 | Overdue list — "Flag cleared this week" group (§5.7), "Was N days late" / "Cleared — moved to Done …" / "Cleared — due date moved …" copy | `FE/src/features/overdue/OverdueListPage.tsx` — a dashed placeholder block under the "Flag cleared this week" heading, text "— tasks cleared this week", the whole block is a button (`aria-disabled`) wired to `useComingSoon`/`ComingSoon` | An overdue-cleared history event (or equivalent) carrying: the date it was flagged, the date/reason it was cleared, and whether clearing was "finished" vs. "due date moved" | Read | No (a new `TaskHistoryEvent` value + write-path entry, not a schema rewrite) — but is backend application work, not just a read |
| B2 | My tasks — "You finished N tasks this week" footer line | `FE/src/features/my-tasks/MyTasksPage.tsx` — footer strip, text "— tasks finished this week" + a "Show" button wired to `useComingSoon`/`ComingSoon` | A per-user, dated aggregate: count of `StatusChanged → Done` events for the caller in the last 7 days | Read | No (aggregate query), but needs new service/endpoint surface, e.g. `GET /tasks/me/finished-this-week` or similar — not designed here, left to the backend lead |
| B3 | Overdue list — "Last check: today, 00:05" | `FE/src/features/overdue/OverdueListPage.tsx` — a second line in the existing info card, "Last check: —", the `—` is a button wired to `useComingSoon`/`ComingSoon` | Either expose the scheduler's last successful run timestamp, or decide "tonight at 00:05" copy without a real value is acceptable — needs a decision, not just a read | Read | Possibly none (could be a config/health value) |
| B4 | Team capacity (A2) / Assign picker grouping (A8) — the configured in-progress limit, shown as "max —" | `FE/src/features/team-capacity/TeamCapacityPage.tsx` — header shows "`max` —" (the `—` is a button wired to `useComingSoon`/`ComingSoon`), replacing the literal `IN_PROGRESS_LIMIT` constant in that one display. (The Board toolbar's own copy of this stub was removed 2026-11-13 along with the rest of `TeamCapacityPopover.tsx` — see A9.) The constant itself (`useTeamCapacity.ts`) is **unchanged** and still drives the per-row `n/3`/meter/"Full" grouping internally (an already-approved, separately-flagged assumption — showing `—` there too would break a working, approved feature; only the *display of the limit as a confirmed fact* was the target of this stub) | A read-only field or endpoint exposing the configured `MaxInProgressPerAssignee` (e.g. a field on `GET /users` or `GET /tasks/board`, or a tiny `GET /config/workflow` — not designed here) | Read | No |
| B5 | My tasks — "Sent back by `<name>` for rework" chip (§5.8) | Not stubbed — reverted to no chip (the row still shows everything else); no dedicated control exists to wire "Coming soon" to, since the chip was additive, not a replaced frame | A field already on `TaskSummary`/the board response (e.g. `lastReworkActor`), or a batch "history summary" endpoint — not a per-task `GET …/history` call per visible row | Read | No |
| B6 | Done card "Approved by `<name>` · `<date>`" (§4 TaskCard) | Not stubbed — reverted to the original "Locked · `<date>`" line (`FE/src/features/board/components/TaskCard.tsx`) | A field already on `TaskSummary`/the board response (e.g. `lastApprovedBy`), or a batch endpoint — not a per-task `GET …/history` call per visible Done card | Read | No |
| B7 | Board/List toolbar — Sort menu's "Due date" / "Recently updated" options (§4 "Sort button") | `FE/src/features/board/components/SortMenu.tsx` — a real `role="menu"` with "Priority" as the current selection; picking either other option is wired to `useComingSoon`/`ComingSoon` and makes no request | A server-side sort parameter on `GET /tasks/board`/`GET /tasks` (e.g. `sortBy=dueDate\|updatedAt`). Sorting client-side over the already-loaded page would be wrong once a column has more than one page (`BoardColumnPageSize` 50, Done 20) — same N+1/partial-data class of problem as B5/B6, just for ordering instead of an extra field | Read | No |

Every stub above except B5/B6 replaces a real, visible control or adds one where the design draws
one (a dashed block, a footer line, a header caption) — none invent a number. B5/B6 have no control
to stub: they were additive chips on rows that work fine without them, so reverting is the whole
fix, not "revert + also add a disabled button nobody asked for."

## C — contradicts a signed-off contract/decision, not built

| # | Item | Why not |
|---|---|---|
| C1 | Reviewer field anywhere (board card `TF-9 · reviewer Minh`, Task detail properties panel, New task modal) | F01 D5 — no reviewer role; anyone but the current assignee may move a task out of Review. Adding a Reviewer field would contradict the signed-off workflow contract. `SPEC.md` §10 point 1, unchanged. |
| C2 | Assign picker reassignment flow / `Task.Move.CapacityReached` opening an inline picker (§5.6 dialog 2, "Lan is at capacity") | `docs/features/f02-capacity.md` AC22 requires a plain refusal + rollback for this exact code; building the picker for it was tried once (slice 4), found to violate AC22, and reverted 2026-11-09. `SPEC.md` §10 point 8, unchanged. |

## Summary

- **A: 10** items built: Inbox, Team capacity page + popover + AvatarStack + capacity pill, List
  view, search, the three keyboard shortcuts, Assign picker grouping, Overdue "Due today". Two
  (A11, A12) were built and then reverted to category B (see below) after a closer look found an
  N+1 fetch pattern in both.
- **Completeness re-audit (2026-11-11, second pass):** every A item states its endpoint(s)/field(s)
  and whether its number/list can be incomplete given the board's page-1-per-column cap. One real
  gap found and fixed: List view (A3) had no "Show more" of its own. Two caveat-display gaps found
  and now fixed (third pass): Team capacity's and search's own "may be incomplete" notes are built
  (`useTeamCapacity().mayBeIncomplete`, `BoardSearch`'s `mayBeIncomplete` prop).
- **B: 6** items as of the 2026-11-11 session. Four are real "Coming soon" stubs built that session
  (B1–B4: flag-cleared, finished-this-week, last-check, the in-progress limit display) — every
  clickable control shows `—` for the data and makes no API call (confirmed via Playwright's network
  log, all four theme/width combinations). Two (B5, B6) are reverted features, not stubs: My tasks
  "Sent back by" and the Done card "Approved by" line both turned out to need one extra history
  request per rendered row/card — removed rather than kept as a scaling N+1 pattern, with no
  replacement control (nothing was there to stub; the row/card works fine without the extra line).
  **B: 7** as of 2026-11-13 — B7 (Sort menu's non-Priority options) added once the Sort control was
  fixed to be an actual, operable menu; A9 (Board toolbar AvatarStack/capacity pill/Team popover) was
  removed the same day, see A9's own row above, not added here as it needs no backend addition.
- **C: 2** items, not built because they contradict a signed-off contract (F01 D5, F02 AC22) —
  never build these without a new decision overriding the one named.
