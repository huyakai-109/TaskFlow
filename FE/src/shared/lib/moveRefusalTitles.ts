/**
 * Dialog titles for the drag refusals that get a blocking `role="alertdialog"`
 * (`docs/design/SPEC.md` §5.6 and `docs/design/png/BlockedActions.png`). The body text reuses
 * `describeErrorCode` (`FE/src/api/errorMessages.ts`), so there is still one
 * error-code → copy map in the app — this only adds the short heading.
 *
 * `Task.Move.Conflict` and `Task.Move.NotFound` are deliberately absent: by
 * the time either fires the board has already been refetched (`docs/ROADMAP.md`
 * slice 1.6 — "a conflict should refetch the board rather than silently
 * failing"), so those get a toast instead of a dialog (`MoveToast` in
 * `BoardPage`/`TaskDetailPage`).
 *
 * `Task.Move.AssigneeRequired` is deliberately absent: `BoardPage`/`TaskDetailPage`
 * open `AssignAndMoveDialog` for that code instead of this dialog (F02, slice 3.4).
 *
 * `Task.Move.CapacityReached` stays a plain refusal here, not the picker —
 * `docs/features/f02-capacity.md` AC22 requires the client to "show the
 * right message and roll the card back" for this exact code, and slice 3.5
 * (sorting/reassignment affordances) was rejected 2026-11-05. The UI
 * redesign briefly folded it into `AssignAndMoveDialog` (slice 4) before
 * this was checked against AC22; reverted 2026-11-09.
 */
export const MOVE_REFUSAL_TITLES: Record<string, string> = {
  'Task.Move.InvalidTransition': 'Move it one step at a time',
  'Task.Move.SameStatus': 'Already there',
  'Task.Move.ActorIsAssignee': "You're the assignee",
  'Task.Move.CapacityReached': 'At capacity',
}

export const DEFAULT_REFUSAL_TITLE = 'The move could not be completed'
