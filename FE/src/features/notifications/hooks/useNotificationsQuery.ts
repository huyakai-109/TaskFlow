import { useInfiniteQuery } from '@tanstack/react-query'

import { getNotifications } from '../../../api/notifications'
import type { PaginationResultRes, TaskNotification } from '../../../api/types'

/** One page of the bell popover's list (F06 D3 "the bell popover pages its own list"). */
export const NOTIFICATIONS_PAGE_SIZE = 20

export const NOTIFICATIONS_QUERY_KEY = ['notifications', 'list'] as const

/**
 * Both read and unread notifications, newest first (`GET /notifications`
 * with no `isRead` filter — the API returns both; see
 * `docs/features/f01-workflow.md` T13). Previously fetched with `isRead:
 * false`, so a notification disappeared the moment it was marked read — not
 * an API limitation, a client-side filter choice, found and fixed earlier
 * (`docs/design/COMING-SOON.md`).
 *
 * Paged (`useInfiniteQuery`, F06 D3) instead of one capped page of 20: the
 * popover loads the next page itself as the user scrolls near the bottom
 * (`NotificationBell`'s `IntersectionObserver`), same `start`/`length`/
 * `pagination.totalPages` contract and the same page-number math as
 * `useColumnPagesQuery`/`useOverdueTasksQuery`. The unread badge is a
 * separate, smaller query (`useUnreadNotificationCountQuery`) — it must never
 * be derived from however many pages happen to be loaded here.
 */
export function useNotificationsQuery() {
  return useInfiniteQuery<PaginationResultRes<TaskNotification[]>>({
    queryKey: NOTIFICATIONS_QUERY_KEY,
    queryFn: ({ pageParam, signal }) => {
      const pageNumber = pageParam as number
      const start = (pageNumber - 1) * NOTIFICATIONS_PAGE_SIZE + 1
      return getNotifications(undefined, start, NOTIFICATIONS_PAGE_SIZE, signal)
    },
    initialPageParam: 1,
    getNextPageParam: (lastPage) => {
      const pagination = lastPage.pagination

      if (pagination == null || pagination.pageNumber >= pagination.totalPages) {
        return undefined
      }

      return pagination.pageNumber + 1
    },
    refetchInterval: 30_000,
  })
}
