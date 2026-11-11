# UI completion — A/B/C classification and "Coming soon" inventory

Written for the `feature/ui-complete` task (2026-11-11), which reopened the scope cuts recorded
in `docs/design/SPEC.md` §10 points 2–6 and `docs/ROADMAP.md` R15. This is the input for any
future backend work: every **B** row names the endpoint/field it needs.

Legend — **A**: real with the current API (reads data already returned by an existing endpoint).
**B**: needs a backend addition (new endpoint, field, or migration) — gets a "Coming soon"
control instead. **C**: contradicts a signed-off contract or decision — not built, ever, without a
new decision overriding the one named.

## A — real, buildable with the current API

| # | Item | Current status (this session) | Data source |
|---|---|---|---|
| A1 | Inbox page (`/inbox`) | **Built** | `GET /notifications` (existing, supports `isRead`, pagination) |
| A2 | Team capacity page (`/team-capacity`) | **Built** | `GET /tasks/board`'s `InProgress` column (`assignee` per task) reduced to a per-user count, plus `GET /users` for the full roster (so a 0/3 person still appears) |
| A3 | Board List view (segmented control) | **Built** | `useBoardQuery` — same data as the Board view, rendered as one flat table |
| A4 | Search (`⌘K`) quick-switch to a task | **Built** — client-side match over already-loaded board tasks (title/`#id`), not a server search; there is no search endpoint | `useBoardQuery` |
| A5 | Keyboard shortcut `C` — new task | **Built**, Board page only | n/a (UI-only) |
| A6 | Keyboard shortcuts `J`/`K` — next/previous task | **Built**, Task detail page only, within the task's own status column (from the board's already-loaded data) | `useBoardQuery` |
| A7 | Keyboard shortcut `⌘↵`/`Ctrl+Enter` — primary action | **Built**, Task detail (first allowed Move) and New task dialog (submit) | n/a |
| A8 | Assign picker "HAS ROOM · MOST FIRST" / "AT CAPACITY" grouping + disabled "Full" rows (§5.5) | **Not built this session** — unblocked, same per-user count as A2 | `GET /tasks/board` InProgress column + `GET /users` |
| A9 | Board toolbar AvatarStack + capacity pill + Team popover (§5.1, §5.2) | **Not built this session** — unblocked, same count as A2; only the standalone Team capacity page was built, not the popover-under-avatar-stack variant | same as A2 |
| A10 | Overdue list — "Due today, not overdue yet" group (§5.7) | **Not built this session** — unblocked: a `GET /tasks` row with `isOverdue=false` and `dueDate` = today | `GET /tasks` (no new filter needed — filter client-side on the already-returned `dueDate`/`isOverdue`) |
| A11 | My tasks — "Sent back by `<name>` for rework" chip (§5.8) | **Not built this session** — unblocked: read the task's own history (`GET /tasks/{id}/history`) for the latest `Review → InProgress` entry's `actorName`; bounded fetch (≤3 In-progress + Review cards, not board-wide) | `GET /tasks/{id}/history` |
| A12 | Done card "Approved by `<name>` · `<date>`" (§4 TaskCard) | **Not built this session** — unblocked, same per-task history read as A11, for each Done card shown | `GET /tasks/{id}/history` |

All of A2, A8, A9, A10 (where it reads `InProgress`/board columns) inherit the same accepted
limitation as My tasks (SPEC §10 point 9): `GetBoard` caps each column at `BoardColumnPageSize`
(50) and only loads more once that column's own "Show more" is used, so a person or task beyond
page 1 of a column is silently under-counted/missing. Not a bug — documented, same as point 9.

## B — needs a backend addition (real "Coming soon" stubs)

| # | Item | Behaviour now | Endpoint/field needed | Read or write | Migration? |
|---|---|---|---|---|---|
| B1 | Overdue list — "Flag cleared this week" group (§5.7), "Was N days late" / "Cleared — moved to Done …" / "Cleared — due date moved …" copy | Nav/section not shown; no stub control exists to replace (there's no single button to wire to "Coming soon" — the whole group is just absent) | An overdue-cleared history event (or equivalent) carrying: the date it was flagged, the date/reason it was cleared, and whether clearing was "finished" vs. "due date moved" | Read | No (a new `TaskHistoryEvent` value + write-path entry, not a schema rewrite) — but is backend application work, not just a read |
| B2 | My tasks — "You finished N tasks this week" footer line | Footer line not shown | A per-user, dated aggregate: count of `StatusChanged → Done` events for the caller in the last 7 days | Read | No (aggregate query), but needs new service/endpoint surface, e.g. `GET /tasks/me/finished-this-week` or similar — not designed here, left to the backend lead |
| B3 | Overdue list — "Last check: today, 00:05" | Not shown | Either expose the scheduler's last successful run timestamp, or derive it is accepted as "tonight at 00:05" copy without a real value — needs a decision, not just a read | Read | Possibly none (could be a config/health value) |

No "Coming soon" control was built for B1–B3 this session: none of them replace an existing,
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

- **A: 12** items identified; **4 built** this session (A1–A4 + the 3 keyboard shortcuts A5–A7
  bring the "built" count to 7 of 12); **5 not built**, all unblocked for a future slice without
  any backend change (A8–A12).
- **B: 3** items, genuinely blocked on backend work. Grouped by what they need: (1) an
  overdue-cleared history event (B1), (2) a dated per-user aggregate endpoint (B2), (3) a
  scheduler-status value or a decision to invent none (B3).
- **C: 2** items, not built because they contradict a signed-off contract (F01 D5, F02 AC22) —
  never build these without a new decision overriding the one named.
