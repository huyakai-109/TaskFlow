import { useQuery } from '@tanstack/react-query'

import { getNotifications } from '../../../api/notifications'

export const UNREAD_NOTIFICATION_COUNT_QUERY_KEY = ['notifications', 'unreadCount'] as const

/**
 * The bell badge's true unread total (F06 D4) — `GET /notifications?isRead=
 * false&start=1&length=1`'s own `pagination.totalItems`, never `result.length`
 * of whatever page(s) `useNotificationsQuery` happens to have loaded. No new
 * endpoint needed: the existing list endpoint's pagination envelope already
 * carries the real total. A separate, one-row query so paging the popover's
 * list (`useNotificationsQuery`) never changes what the badge shows.
 */
export function useUnreadNotificationCountQuery() {
  return useQuery({
    queryKey: UNREAD_NOTIFICATION_COUNT_QUERY_KEY,
    queryFn: async ({ signal }) => {
      const response = await getNotifications(false, 1, 1, signal)
      return response.pagination?.totalItems ?? 0
    },
    refetchInterval: 30_000,
  })
}
