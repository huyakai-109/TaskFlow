import type { AllowedTransition, TaskStatus, TaskSummary } from '../../api/types'

export interface TaskActionView {
  /** `allowedTransitions` filtered to `operation === 'Move'` — every legal next step for this caller. */
  moves: AllowedTransition[]
  /** The `Done → Review` reopen edge, present only when the caller holds `ReopenTasks`. */
  reopen: AllowedTransition | undefined
  /**
   * Whether to render a "Take over" action. Driven strictly by the
   * server's own `canTakeOver` flag (Revision R2 wire changes) — never
   * inferred from role or status client-side. The `status` check below is
   * a defensive mirror of the same Review/Done lock the server already
   * applies when computing `canTakeOver`, not a second source of truth.
   */
  showTakeOver: boolean
}

const ASSIGNMENT_LOCKED_STATUSES: ReadonlySet<TaskStatus> = new Set(['Review', 'Done'])

/**
 * One place that derives which workflow buttons a task's card/detail page
 * shows, from the fields the server already computed
 * (`allowedTransitions`, `canTakeOver`) — used by `WorkflowStrip`,
 * `CardMoveAction`'s callers and My tasks, so the "which buttons" question
 * is answered once instead of re-read inline at each call site.
 */
export function getTaskActionView(task: Pick<TaskSummary, 'allowedTransitions' | 'canTakeOver' | 'status'>): TaskActionView {
  return {
    moves: task.allowedTransitions.filter((t) => t.operation === 'Move'),
    reopen: task.allowedTransitions.find((t) => t.operation === 'Reopen'),
    showTakeOver: task.canTakeOver && !ASSIGNMENT_LOCKED_STATUSES.has(task.status),
  }
}
