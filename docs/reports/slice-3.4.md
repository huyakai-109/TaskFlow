# Slice 3.4 — Capacity UI

## Plan (written before coding)

**Goal.** Four new F02 error codes are mapped to copy; `*.CapacityReached` shows the
server's own `error` string (already name + limit, per T4 sign-off) in the existing
blocking refusal dialog and the card rolls back (reuses F01's optimistic-update
rollback, unchanged); `Task.Move.AssigneeRequired` opens an assignee picker instead of
a plain refusal, chains `PUT …/assignee` then `PUT …/status` on selection
("Assign to … and start", `docs/ui.md` line 90); the picker (new chain dialog, plus
the two existing pickers left over from slice 1.7's stopgap) reads `GET /users`
instead of `useKnownAssignees`, which is deleted once nothing calls it.

**Files expected to change:**
- `FE/src/api/types.ts` — `UserListItem` type for `GET /users`.
- `FE/src/api/users.ts` (new) — `getUsers()`.
- `FE/src/shared/hooks/useUsersQuery.ts` (new) — TanStack Query wrapper.
- `FE/src/shared/hooks/useAssignAndMoveTaskMutation.ts` (new) — chains assign → move,
  reports which step failed.
- `FE/src/shared/components/AssignAndMoveDialog.tsx` (new) — the picker for the
  `AssigneeRequired` chain, modelled on `AssigneePickerDialog`/`MoveRefusalDialog`.
- `FE/src/api/errorMessages.ts` — four new codes; `describeErrorCode` takes an
  optional raw server message, used only for `*.CapacityReached`.
- `FE/src/shared/lib/moveRefusalTitles.ts` — a title for `Task.Move.CapacityReached`.
- `FE/src/shared/hooks/useMoveTaskMutation.ts` — `onRefused` gains the move
  variables (so a caller can re-open with the same `taskId`/`toStatus`/`version`)
  and the raw server message.
- `FE/src/shared/components/MoveRefusalDialog.tsx` — optional `message` override.
- `FE/src/features/task-detail/hooks/useAssignTaskMutation.ts` — same message
  threading as move.
- `FE/src/features/task-detail/components/AssigneePickerDialog.tsx` — `useUsersQuery`
  instead of `useKnownAssignees`; raw-message support for its own `CapacityReached`.
- `FE/src/shared/components/AssigneeSelect.tsx` — `useUsersQuery` instead of
  `useKnownAssignees`.
- `FE/src/features/board/BoardPage.tsx`, `FE/src/features/task-detail/TaskDetailPage.tsx`
  — wire the new dialog into both places a move can hit `AssigneeRequired` (drag and
  the button path).
- `FE/src/features/board/components/NewTaskDialog.tsx` — drop the stopgap hint copy
  ("only people already assigned…") now that the picker is the real roster.
- `FE/src/shared/hooks/useKnownAssignees.ts` — deleted (no remaining caller).
- `docs/ROADMAP.md` — slice 3.4 row → Built.

**Verify:** `npm --prefix FE run typecheck`, `run lint`, `run build`. No Vitest in
this repo yet (per `CLAUDE.md`), so no automated UI test; real-browser pass is
attempted if the backend + dev server can be brought up, else `NOT VERIFIED` with
the reason.

## Result

Built as planned, with one addition not foreseen at planning time: `MoveRefusalDialog`,
`useMoveTaskMutation` and `useAssignTaskMutation` needed to thread the server's raw
`error` string through to the dialog (previously only the bare `errorCode` reached
the UI), because `*.CapacityReached`'s contract is to show that exact server-built
sentence (name + limit), not a client-authored one. `describeErrorCode` gained an
optional second parameter for this and is otherwise unchanged for every other code.

**Files changed**
- `FE/src/api/types.ts` — `UserListItem` (`{ id, userName, role }`).
- `FE/src/api/users.ts` (new) — `getUsers()`.
- `FE/src/shared/hooks/useUsersQuery.ts` (new).
- `FE/src/shared/hooks/useAssignAndMoveTaskMutation.ts` (new) — chains assign → move,
  reports `{ step: 'assign' | 'move', code, message }` on failure; always invalidates
  `['board']`/`['task', id]` on error because the assign half may have already landed.
- `FE/src/shared/components/AssignAndMoveDialog.tsx` (new) — the `AssigneeRequired`
  picker, "Assign to … and start" per button, no "Unassigned" option.
- `FE/src/api/errorMessages.ts` — four new codes; `describeErrorCode(code, rawMessage?)`
  prefers `rawMessage` for `Task.Move.CapacityReached` / `Task.Assign.CapacityReached`
  only; `describeError` passes `error.message` through for every code now (behaviour
  unchanged for non-capacity codes, since they ignore the second argument).
- `FE/src/shared/lib/moveRefusalTitles.ts` — title for `Task.Move.CapacityReached`
  ("At capacity"); `Task.Move.AssigneeRequired` stays absent (routed to the new
  dialog instead of this one).
- `FE/src/shared/hooks/useMoveTaskMutation.ts` — `onRefused` now receives the full
  `MoveVariables` (so a caller can re-open with the same `taskId`/`toStatus`/`version`)
  plus the raw message, instead of just `taskTitle`.
- `FE/src/shared/components/MoveRefusalDialog.tsx` — optional `message` prop.
- `FE/src/features/task-detail/hooks/useAssignTaskMutation.ts` — `onRefused` gains
  the raw message.
- `FE/src/features/task-detail/components/AssigneePickerDialog.tsx` — `useUsersQuery`
  instead of `useKnownAssignees`; loading/error states for the now-async roster;
  `errorMessage` prop for its own `CapacityReached` (reassigning an `InProgress` task).
- `FE/src/shared/components/AssigneeSelect.tsx` — `useUsersQuery` instead of
  `useKnownAssignees`; disabled + "Loading…"/"Could not load" options while the
  query is pending/errored (it was synchronous before, reading the board cache).
- `FE/src/features/board/BoardPage.tsx` — `moveMutation.onRefused` branches on
  `Task.Move.AssigneeRequired` to open `AssignAndMoveDialog` (remembers
  `taskId`/`toStatus`/`taskTitle`/`version` from the failed move's own variables);
  wires `useAssignAndMoveTaskMutation`, closing the dialog on success, toasting on a
  move-step failure (assignment already landed), showing the error inline on an
  assign-step failure.
- `FE/src/features/task-detail/TaskDetailPage.tsx` — same wiring for the move button
  path (`WorkflowStrip`'s "Move to In Progress"), plus `assignMutation`'s existing
  `AssigneePickerDialog` gets the same `errorMessage` threading.
- `FE/src/features/board/components/NewTaskDialog.tsx` — dropped the slice-1.7 hint
  ("only people already assigned… no user directory yet"), now untrue.
- `FE/src/shared/hooks/useKnownAssignees.ts` — deleted; its only two callers
  (`AssigneeSelect`, `AssigneePickerDialog`) both moved to `useUsersQuery`.
- `docs/ROADMAP.md` — slice 3.4 row → Built.

**Design notes / decisions made while building**
- `Task.Move.AssigneeRequired` only ever occurs on a move targeting `InProgress`
  (the only `Move` edge needing `RequiresAssignee`, per F02 T3's "no-op reassignment"
  reading and D5), so `AssignAndMoveDialog`'s hardcoded "...and start" copy is safe —
  it is never shown for any other target status.
- On a move-step failure after a successful assign (e.g. the limit filled while the
  user was picking), the dialog closes and a toast explains the move did not go
  through, per `docs/ui.md` line 90 ("if the second fails, say so — the assignment
  stays"); the board/task queries are already invalidated so the UI reflects the new
  assignee even though the status did not change.
- No slice 3.5 scope added anywhere: no sorting by room, no "N/3" counts, nobody
  disabled in any picker — `GET /users` carries no load data to show even if we
  wanted to.

**Verification**
```
Typecheck: PASS   npm --prefix FE run typecheck  (tsc -b --noEmit, no errors)
Lint:      PASS   npm --prefix FE run lint       (oxlint; one pre-existing warning,
                   react-hooks/exhaustive-deps in useDialogA11y.ts, unrelated to this
                   change and not touched)
Build:     PASS   npm --prefix FE run build      (tsc -b && vite build, 123 modules,
                   built in 2.21s)
```

**NOT VERIFIED:** actual UI behaviour in a browser — loading/empty/error/success
states of the new dialog, the assign→move chain against a real backend, drag-and-drop
triggering `AssigneeRequired`, keyboard operation and focus return on
`AssignAndMoveDialog`, and the 390/768/1440 widths. Neither the backend nor
`npm --prefix FE run dev` was started this session (would need MySQL/Redis/MinIO up
and the API running); only typecheck/lint/build were run, as called out in the task.
This is the same gap `qa-lead`'s independent verification is expected to close for
the UI half, the way it already closed it for slices 3.1–3.3's backend half.

**Risks / open items**
- `AssignAndMoveDialog`'s roster has no "has room" ordering (by design, slice 3.5
  rejected) — on a board with many users this could be a long alphabetical list with
  no indication of who can actually accept more work before the capacity check runs
  server-side on selection. Acceptable per the user's explicit scope cut, but worth
  the user knowing it is a UX trade-off, not an oversight.
- `useAssignAndMoveTaskMutation`'s thrown-value shape (`{ step, error }`) is an
  internal implementation detail of that one hook, not a general pattern — flagged
  here in case a reviewer wonders why it isn't a thrown `Error` subclass; a plain
  tagged object was simpler and `onError`'s `TError` generic does not require `Error`.

## Review

`Review: APPROVE on cb27821, 2026-11-05`

Re-verified independently: re-ran `npm --prefix FE run typecheck` (PASS, clean), `npm --prefix FE run lint` (PASS, same single pre-existing `useDialogA11y.ts` warning, nothing new), `npm --prefix FE run build` (PASS, 123 modules, no errors). Read every changed file (`errorMessages.ts`, `types.ts`, `users.ts`, `useUsersQuery.ts`, `useAssignAndMoveTaskMutation.ts`, `AssignAndMoveDialog.tsx`, `AssigneePickerDialog.tsx`, `AssigneeSelect.tsx`, `MoveRefusalDialog.tsx`, `moveRefusalTitles.ts`, `BoardPage.tsx`, `TaskDetailPage.tsx`, `NewTaskDialog.tsx`, `useAssignTaskMutation.ts`, `useMoveTaskMutation.ts`) against T4 and `docs/ui.md`.

### Must fix

None.

### Recommendations

None beyond what the implementer already flagged in *Risks / open items* above (no "has room" ordering in the roster — explicit scope cut; the `{step, error}` thrown-value shape — a reasonable internal choice).

### Pre-existing / out of scope

- None new. `useKnownAssignees.ts` confirmed fully removed — `grep -rn "useKnownAssignees" FE/src/` returns only two comments referencing it historically, no remaining imports or call sites.

### Specific checks

- Rollback on any move refusal (including the two new F02 codes) is unconditional in `useMoveTaskMutation`'s `onError`, which runs before the code-specific branching — F01 AC20 / F02 AC22 hold for every code, not just the ones this slice added.
- `*.CapacityReached` shows the server's own `error` string verbatim (`describeErrorCode`'s `CAPACITY_REACHED_CODES` check), never a client-composed message — matches T4's sign-off exactly.
- `Task.Move.AssigneeRequired` routing to `AssignAndMoveDialog` instead of `MoveRefusalDialog` is handled identically in both `BoardPage.tsx` (drag) and `TaskDetailPage.tsx` (button) — no divergence between the two entry points.
- Assign-then-move chaining uses the assign step's own returned `version`, not the stale one the failed move carried — correct per the optimistic-concurrency contract (a stale version would itself trigger a spurious `Conflict` on the move step).
- No new dependency, no barrel file, no cross-feature import (`AssignAndMoveDialog`/`useAssignAndMoveTaskMutation`/`useUsersQuery` correctly live in `shared/` since both `board` and `task-detail` need them), no `localStorage` use, no API contract change.

Not reviewed (same gap the implementer flagged): actual browser behavior — loading/empty/error/success states of the new dialog, the full assign→move flow against a running backend, drag-triggered `AssigneeRequired`, focus/keyboard behavior, and the 390/768/1440px breakpoints. Scheduled for `qa-lead` next.

## Independent QA verification (qa-lead, gate C, 2026-11-05)

**Verdict: READY, no Must-fix.** Re-ran typecheck/lint/build independently (all PASS, identical to the claimed numbers: same single pre-existing `useDialogA11y.ts` warning, same 123-module build). Confirmed `git show --stat cb27821` matches the report's file list 1:1, no scope creep. Every item in T4 + the user's checklist individually confirmed MET by reading the code directly: the 4 error codes and `describeErrorCode`'s raw-message preference; unconditional rollback in `useMoveTaskMutation` (runs before the code branch, so it isn't limited to the 2 new codes); the assign→move chain using the assign step's own returned `version`; identical `AssigneeRequired` routing on both `BoardPage` and `TaskDetailPage`; the picker using `GET /users` with `useKnownAssignees` fully deleted (`grep` confirms only historical doc-comment references remain); no slice 3.5 scope (no room-sorting/disabling anywhere); no new dependency (`package.json`/`package-lock.json` diff empty); no cross-feature import, no barrel file.

Two pre-existing, out-of-scope observations (not introduced by 3.4, not blocking): `MoveRefusalDialog`'s card lacks `w-full` unlike the two newer dialogs (slice 1.6 cosmetic inconsistency); `useDialogA11y` has no Tab focus-trap (pre-existing pattern across all dialogs, flagged for awareness only).

**Not tested, explicitly**: actual browser behavior — this QA pass was scoped to static checks only (a permission classifier blocked an earlier attempt to start the backend/dev server, out of caution around `AutoMigration=true` touching a real database). Real loading/empty/error/success rendering, the assign→move chain against a running backend, drag-and-drop triggering `AssigneeRequired` in practice, interactive keyboard/focus behavior, and the three breakpoints remain unverified in any session for this slice. Recommended before marking Unit 3 "Done": a manual smoke pass with the dev stack running, per `workflow.md`'s "Ready for Review" (verified statically) vs. "Done" (accepted by the user) distinction.

