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

/** `AssigneeRes` */
export interface Assignee {
  id: number
  userName: string
}

/**
 * `UserRes` — `GET /users` (`docs/features/f02-capacity.md` T4), the assignee
 * picker's roster. `role` is the role's display name (`UserResDto.Role`), a
 * plain string on the wire, not an enum `JsonStringEnumConverter` controls —
 * unlike `TaskStatus`/`TaskPriority` above.
 */
export interface UserListItem {
  id: number
  userName: string
  role: string
}

/**
 * `AllowedTransitionRes` — one legal next move for *this* caller, with the
 * transition table, permissions and the "someone else moves it out of Review"
 * rule already applied server-side (F01 D10). `operation` distinguishes an
 * ordinary move from the reopen operation, which `requiresReason`. Never
 * re-derive the transition table on the client — only read this list.
 */
export interface AllowedTransition {
  status: TaskStatus
  operation: 'Move' | 'Reopen'
  requiresReason: boolean
}

/** `TaskSummaryRes` — board card, per `docs/features/f01-workflow.md` T5. */
export interface TaskSummary {
  id: number
  title: string
  status: TaskStatus
  priority: TaskPriority
  assignee: Assignee | null
  /** Calendar date, `yyyy-MM-dd`, or null. Never a timestamp — see F05. */
  dueDate: string | null
  isOverdue: boolean
  /** UTC instant, set when the task was approved into `Done`. */
  completedAt: string | null
  /** Concurrency token; sent back on every write (not used until slice 1.6). */
  version: string
  allowedTransitions: AllowedTransition[]
}

/** `BoardColumnRes` — always present for all five statuses, even when empty. */
export interface BoardColumn {
  status: TaskStatus
  items: TaskSummary[]
  pagination: PaginationRes | null
}

/** `BoardRes` */
export interface Board {
  columns: BoardColumn[]
}

/** `TaskDetailRes` — `TaskSummaryRes` plus the detail-only fields (T5 endpoint 2). */
export interface TaskDetail extends TaskSummary {
  description: string | null
  createdAt: string
  createdBy: number
  updatedAt: string
}

/** `TaskHistoryEvent` enum, wire names (`f01-workflow.md` T1). */
export type TaskHistoryEvent =
  | 'Created'
  | 'StatusChanged'
  | 'AssigneeChanged'
  | 'PriorityChanged'
  | 'DueDateChanged'
  | 'Reopened'
  | 'OverdueMarked'
  | 'Deleted'

/**
 * `TaskHistoryRes` — one append-only entry (F04). `message` is already a
 * human-readable sentence built server-side (`TaskHistoryMessages.Build`);
 * the client renders it as-is rather than re-deriving copy from the
 * structured fields.
 */
export interface TaskHistoryEntry {
  id: number
  taskId: number
  event: TaskHistoryEvent
  actorUserId: number | null
  actorName: string | null
  fromValue: string | null
  toValue: string | null
  reason: string | null
  occurredAt: string
  occurredAtLocal: string
  message: string
}

/** `TaskNotificationEvent` enum — `ReturnedForRework` is the only MVP value (F01 D7). */
export type TaskNotificationEvent = 'ReturnedForRework'

/** `NotificationRes` */
export interface TaskNotification {
  id: number
  taskId: number
  event: TaskNotificationEvent
  isRead: boolean
  readAt: string | null
  createdAt: string
}
