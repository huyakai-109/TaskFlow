/**
 * Dialog titles for the drag refusals that get a blocking `role="alertdialog"`
 * (`docs/ui/screens/BlockedActions.dc.html`). The body text reuses
 * `describeErrorCode` (`FE/src/api/errorMessages.ts`), so there is still one
 * error-code → copy map in the app — this only adds the short heading.
 *
 * `Task.Move.Conflict` and `Task.Move.NotFound` are deliberately absent: by
 * the time either fires the board has already been refetched (`docs/ROADMAP.md`
 * slice 1.6 — "a conflict should refetch the board rather than silently
 * failing"), so those get a toast instead of a dialog (`MoveToast` in
 * `BoardPage`/`TaskDetailPage`).
 *
 * `Task.Move.AssigneeRequired` / `.CapacityReached` (F02's capacity guard) are
 * not here: F02 is not built yet, the backend cannot return them today, and
 * `TaskControllerMS.Move` has no such members on this branch.
 */
export const MOVE_REFUSAL_TITLES: Record<string, string> = {
  'Task.Move.InvalidTransition': 'Move it one step at a time',
  'Task.Move.SameStatus': 'Already there',
  'Task.Move.ActorIsAssignee': "You're the assignee",
}

export const DEFAULT_REFUSAL_TITLE = 'The move could not be completed'
