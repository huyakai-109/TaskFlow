import { request } from './client'
import type { PaginationResultRes, ResultRes, TaskNotification } from './types'

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

/**
 * `PUT /notifications/read-all` (F06 "API") — one request for the whole
 * batch, never one `PUT` per id. `upToId` is the newest id the caller has
 * seen (the first row of the newest-first list); omitting it marks every
 * unread row. Returns the count the server actually marked.
 */
export async function markAllNotificationsRead(upToId?: number): Promise<number> {
  const envelope = await request<ResultRes<number>>('/notifications/read-all', {
    method: 'PUT',
    body: { upToId: upToId ?? null },
  })

  return envelope.result ?? 0
}
