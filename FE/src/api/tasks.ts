import { request } from './client'
import type {
  Board,
  PaginationResultRes,
  ResultRes,
  TaskDetail,
  TaskHistoryEntry,
  TaskPriority,
  TaskStatus,
  TaskSummary,
} from './types'

/** Body of `POST /tasks` (`TaskCreateReq`). */
export interface CreateTaskInput {
  title: string
  description: string | null
  priority: TaskPriority
  /** `yyyy-MM-dd`, or null for no due date. */
  dueDate: string | null
  assigneeId: number | null
}

/**
 * `GET /tasks/board` (`docs/features/f01-workflow.md` T5 #3). Always returns all
 * five columns, in lifecycle order, even when empty — the client never has to
 * invent a missing column.
 */
export async function getBoard(signal?: AbortSignal): Promise<Board> {
  const envelope = await request<ResultRes<Board>>('/tasks/board', { signal })
  return envelope.result ?? { columns: [] }
}

/**
 * `GET /tasks?status=&start=&length=` (T5 #4) — page 2+ of one board column.
 * `length` must equal the board's `pagination.pageSize` for that column so the
 * page continues exactly where the board's page 1 ended (F03 AC15).
 */
export async function getTaskPage(
  status: TaskStatus,
  start: number,
  length: number,
  signal?: AbortSignal,
): Promise<PaginationResultRes<TaskSummary[]>> {
  return request<PaginationResultRes<TaskSummary[]>>(
    `/tasks?status=${status}&start=${start}&length=${length}`,
    { signal },
  )
}

/** `GET /tasks/{id}` (T5 #2) — the task detail screen's read. */
export async function getTask(id: number, signal?: AbortSignal): Promise<TaskDetail> {
  const envelope = await request<ResultRes<TaskDetail>>(`/tasks/${id}`, { signal })

  if (envelope.result == null) {
    throw new Error(`Task ${id} was not returned`)
  }

  return envelope.result
}

/**
 * `PUT /tasks/{id}/status` (T5 #9) — the board's drag-and-drop move. `version`
 * is the concurrency token the client last rendered; a mismatch comes back as
 * `Task.Move.Conflict` (AC30).
 */
export async function moveTask(id: number, status: TaskStatus, version: string): Promise<TaskDetail> {
  const envelope = await request<ResultRes<TaskDetail>>(`/tasks/${id}/status`, {
    method: 'PUT',
    body: { status, version },
  })

  if (envelope.result == null) {
    throw new Error(`Move of task ${id} returned no task`)
  }

  return envelope.result
}

/** `PUT /tasks/{id}/reopen` (T5 #10) — `Done → Review`, Manager/Admin only, requires a reason (F01 D6). */
export async function reopenTask(id: number, reason: string, version: string): Promise<TaskDetail> {
  const envelope = await request<ResultRes<TaskDetail>>(`/tasks/${id}/reopen`, {
    method: 'PUT',
    body: { reason, version },
  })

  if (envelope.result == null) {
    throw new Error(`Reopen of task ${id} returned no task`)
  }

  return envelope.result
}

/** `POST /tasks` (T5 #1) — new tasks always start in Backlog (server-decided). */
export async function createTask(input: CreateTaskInput): Promise<TaskDetail> {
  const envelope = await request<ResultRes<TaskDetail>>('/tasks', {
    method: 'POST',
    body: input,
  })

  if (envelope.result == null) {
    throw new Error('Create task returned no task')
  }

  return envelope.result
}

/** `PUT /tasks/{id}` (T5 #5) — title/description only; no history entry (F04 scope). */
export async function updateTask(id: number, title: string, description: string | null, version: string): Promise<TaskDetail> {
  const envelope = await request<ResultRes<TaskDetail>>(`/tasks/${id}`, {
    method: 'PUT',
    body: { title, description, version },
  })

  if (envelope.result == null) {
    throw new Error(`Update of task ${id} returned no task`)
  }

  return envelope.result
}

/** `PUT /tasks/{id}/priority` (T5 #6). */
export async function updateTaskPriority(id: number, priority: TaskPriority, version: string): Promise<TaskDetail> {
  const envelope = await request<ResultRes<TaskDetail>>(`/tasks/${id}/priority`, {
    method: 'PUT',
    body: { priority, version },
  })

  if (envelope.result == null) {
    throw new Error(`Update priority of task ${id} returned no task`)
  }

  return envelope.result
}

/** `PUT /tasks/{id}/due-date` (T5 #7) — an explicit `null` clears the due date. */
export async function updateTaskDueDate(id: number, dueDate: string | null, version: string): Promise<TaskDetail> {
  const envelope = await request<ResultRes<TaskDetail>>(`/tasks/${id}/due-date`, {
    method: 'PUT',
    body: { dueDate, version },
  })

  if (envelope.result == null) {
    throw new Error(`Update due date of task ${id} returned no task`)
  }

  return envelope.result
}

/** `PUT /tasks/{id}/assignee` (T5 #8) — `assigneeId: null` unassigns. */
export async function assignTask(id: number, assigneeId: number | null, version: string): Promise<TaskDetail> {
  const envelope = await request<ResultRes<TaskDetail>>(`/tasks/${id}/assignee`, {
    method: 'PUT',
    body: { assigneeId, version },
  })

  if (envelope.result == null) {
    throw new Error(`Assign of task ${id} returned no task`)
  }

  return envelope.result
}

/** `GET /tasks/{taskId}/history` (T5 #12) — oldest first, per the endpoint's contract. */
export async function getTaskHistory(
  taskId: number,
  start = 1,
  length = 100,
  signal?: AbortSignal,
): Promise<PaginationResultRes<TaskHistoryEntry[]>> {
  return request<PaginationResultRes<TaskHistoryEntry[]>>(`/tasks/${taskId}/history?start=${start}&length=${length}`, {
    signal,
  })
}
