import type { TaskStatus } from '../../api/types'

/** Display labels for the five workflow statuses — `docs/design/SPEC.md` "In Progress" over the wire's `InProgress`. */
export const STATUS_LABELS: Record<TaskStatus, string> = {
  Backlog: 'Backlog',
  Todo: 'Todo',
  InProgress: 'In Progress',
  Review: 'Review',
  Done: 'Done',
}
