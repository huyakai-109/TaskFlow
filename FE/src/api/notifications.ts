import { request } from './client'
import type { PaginationResultRes, TaskNotification } from './types'

/** `GET /notifications` (T5 #13) — the caller's own rows only, newest first. */
export async function getNotifications(
  isRead?: boolean,
  start = 1,
  length = 20,
  signal?: AbortSignal,
): Promise<PaginationResultRes<TaskNotification[]>> {
  const params = new URLSearchParams({ start: String(start), length: String(length) })

  if (isRead !== undefined) {
    params.set('isRead', String(isRead))
  }

  return request<PaginationResultRes<TaskNotification[]>>(`/notifications?${params.toString()}`, { signal })
}

/** `PUT /notifications/{id}/read` (T5 #14) — idempotent. */
export async function markNotificationRead(id: number): Promise<void> {
  await request(`/notifications/${id}/read`, { method: 'PUT' })
}
