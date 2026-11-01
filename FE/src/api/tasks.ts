import { request } from './client'
import type { Board, PaginationResultRes, ResultRes, TaskDetail, TaskHistoryEntry, TaskStatus } from './types'

/**
 * `GET /tasks/board` (`docs/features/f01-workflow.md` T5 #3). Always returns all
 * five columns, in lifecycle order, even when empty — the client never has to
 * invent a missing column.
 */
export async function getBoard(signal?: AbortSignal): Promise<Board> {
  const envelope = await request<ResultRes<Board>>('/tasks/board', { signal })
  return envelope.result ?? { columns: [] }
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
