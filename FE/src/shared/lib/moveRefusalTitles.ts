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
 * `Task.Move.AssigneeRequired` and `Task.Move.CapacityReached` are
 * deliberately absent: `BoardPage`/`TaskDetailPage` open `AssignAndMoveDialog`
 * for both codes instead of this dialog — a task with no assignee and a task
 * whose assignee is already full both resolve the same way, reassign and
 * retry (F02, slice 3.4; CapacityReached folded in during the UI redesign's
 * slice 4, approved 2026-11-09).
 */
export const MOVE_REFUSAL_TITLES: Record<string, string> = {
  'Task.Move.InvalidTransition': 'Move it one step at a time',
  'Task.Move.SameStatus': 'Already there',
  'Task.Move.ActorIsAssignee': "You're the assignee",
}

export const DEFAULT_REFUSAL_TITLE = 'The move could not be completed'
