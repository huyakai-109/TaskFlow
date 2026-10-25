/**
 * Mirrors the API's response envelope and the task model.
 *
 * The envelope matches `TaskFlow.Api/Models/Responses/Base` (ASP.NET serialises
 * those types in camelCase). The task shape follows `docs/features/f01-workflow.md`.
 * Field names here are a contract with the API — change them only together with it.
 */

export type TaskStatus = 'Backlog' | 'Todo' | 'InProgress' | 'Review' | 'Done'

export type TaskPriority = 'Urgent' | 'High' | 'Normal'

/** The four columns that F03 orders by priority; `Done` has its own rule. */
export const ACTIVE_STATUSES: readonly TaskStatus[] = ['Backlog', 'Todo', 'InProgress', 'Review']

export const BOARD_COLUMNS: readonly TaskStatus[] = [...ACTIVE_STATUSES, 'Done']

export interface ValidateRes {
  key: string
  errors: string[]
}

/** `ExecutionRes` — the base every response extends. */
export interface ExecutionRes {
  success: boolean
  errorCode?: string | null
  error?: string | null
  validates?: ValidateRes[] | null
}

/** `ResultRes<T>` */
export interface ResultRes<T> extends ExecutionRes {
  result?: T | null
}

/** `PaginationRes` */
export interface PaginationRes {
  pageNumber: number
  pageSize: number
  totalPages: number
  totalItems: number
  currentCount: number
}

/** `PaginationResultRes<T>` */
export interface PaginationResultRes<T> extends ResultRes<T> {
  pagination?: PaginationRes | null
}

/**
 * The fields below come from the agreed task model in F01. The exact JSON of each
 * read endpoint — what a board response nests, and which extra display fields it
 * carries — is NOT agreed yet, so nothing is built against this shape until the
 * API contract for F01 is written. Treat it as the model, not as the wire format.
 */
export interface TaskSummary {
  id: number
  title: string
  status: TaskStatus
  priority: TaskPriority
  assigneeId: number | null
  /** Calendar date, `yyyy-MM-dd`, or null. Never a timestamp — see F05. */
  dueDate: string | null
  isOverdue: boolean
  /** UTC instant, set when the task was approved into `Done`. */
  completedAt: string | null
  /**
   * The statuses *this* caller may move the task to right now, with permissions,
   * the transition table and the "someone else moves it out of Review" rule already
   * applied (F01 D10). The board uses it to decide which drop targets are live —
   * never re-derive it on the client.
   */
  allowedTransitions: TaskStatus[]
}
