import type { InfiniteData } from '@tanstack/react-query'
import { useMutation, useQueryClient } from '@tanstack/react-query'

import { markNotificationRead } from '../../../api/notifications'
import type { PaginationResultRes, TaskNotification } from '../../../api/types'
import { NOTIFICATIONS_QUERY_KEY } from './useNotificationsQuery'
import { UNREAD_NOTIFICATION_COUNT_QUERY_KEY } from './useUnreadNotificationCountQuery'

type NotificationsData = InfiniteData<PaginationResultRes<TaskNotification[]>>

interface MarkAllReadContext {
  previousList: NotificationsData | undefined
  previousUnreadCount: number | undefined
}

function withIdsMarkedRead(data: NotificationsData | undefined, ids: ReadonlySet<number>): NotificationsData | undefined {
  if (data == null) {
    return data
  }

  return {
    ...data,
    pages: data.pages.map((page) => {
      if (page.result == null) {
        return page
      }

      return {
        ...page,
        result: page.result.map((notification) =>
          ids.has(notification.id)
            ? { ...notification, isRead: true, readAt: notification.readAt ?? new Date().toISOString() }
            : notification,
        ),
      }
    }),
  }
}

/**
 * "Mark all as read" (`NotificationBell.tsx`). There is no bulk endpoint
 * (`docs/features/f06-notification-history.md` proposes `PUT
 * /notifications/read-all`, not built yet — tracked as `docs/design/
 * COMING-SOON.md` B8) — this calls the existing idempotent `PUT
 * /notifications/{id}/read` once per id the caller passes in, same stopgap
 * as before. `NotificationBell` only ever passes ids of notifications it has
 * actually loaded (one or more pages of `useNotificationsQuery`): with the
 * popover now paging its own list, the true unread total
 * (`useUnreadNotificationCountQuery`) can be higher than what this button
 * just marked, and the badge is left to show that honestly rather than
 * claim "all" read when some unread rows were never loaded.
 *
 * The cache write is optimistic and batched into one `setQueryData` call per
 * cache (not one write per settled request); only ids still unread are ever
 * passed in, so no id already read gets a redundant request. Rolls back both
 * the paged list and the unread-count cache to their pre-mutate snapshots on
 * any failure, same shape as the single mark-read mutation.
 */
export function useMarkAllNotificationsReadMutation(options: { onRefused?: (error: unknown) => void } = {}) {
  const queryClient = useQueryClient()

  return useMutation<unknown, unknown, number[], MarkAllReadContext>({
    mutationFn: async (unreadIds: number[]) => {
      await Promise.all(unreadIds.map((id) => markNotificationRead(id)))
    },
    onMutate: async (unreadIds) => {
      await queryClient.cancelQueries({ queryKey: ['notifications'] })
      const previousList = queryClient.getQueryData<NotificationsData>(NOTIFICATIONS_QUERY_KEY)
      const previousUnreadCount = queryClient.getQueryData<number>(UNREAD_NOTIFICATION_COUNT_QUERY_KEY)
      const idSet = new Set(unreadIds)

      queryClient.setQueryData<NotificationsData>(NOTIFICATIONS_QUERY_KEY, (current) => withIdsMarkedRead(current, idSet))
      queryClient.setQueryData<number>(UNREAD_NOTIFICATION_COUNT_QUERY_KEY, (current) =>
        current == null ? current : Math.max(0, current - idSet.size),
      )

      return { previousList, previousUnreadCount }
    },
    onError: (error, _unreadIds, context) => {
      if (context?.previousList !== undefined) {
        queryClient.setQueryData(NOTIFICATIONS_QUERY_KEY, context.previousList)
      }

      if (context?.previousUnreadCount !== undefined) {
        queryClient.setQueryData(UNREAD_NOTIFICATION_COUNT_QUERY_KEY, context.previousUnreadCount)
      }

      options.onRefused?.(error)
    },
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: ['notifications'] })
    },
  })
}
