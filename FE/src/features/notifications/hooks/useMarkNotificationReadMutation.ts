import type { InfiniteData } from '@tanstack/react-query'
import { useMutation, useQueryClient } from '@tanstack/react-query'

import { markNotificationRead } from '../../../api/notifications'
import type { PaginationResultRes, TaskNotification } from '../../../api/types'
import { NOTIFICATIONS_QUERY_KEY } from './useNotificationsQuery'
import { UNREAD_NOTIFICATION_COUNT_QUERY_KEY } from './useUnreadNotificationCountQuery'

type NotificationsData = InfiniteData<PaginationResultRes<TaskNotification[]>>

interface MarkReadContext {
  previousList: NotificationsData | undefined
  previousUnreadCount: number | undefined
}

/** Marks every id in `ids` read across every already-loaded page, in place, keeping page order. */
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
 * Marks one notification read. Optimistic: the bell's badge and that row's
 * read styling (`NotificationBell`) update immediately from the local cache
 * write below, not after the request round-trips — `onError` rolls both the
 * paged list cache and the separate unread-count cache back to their
 * snapshots and reports the failure through `onRefused`, same rollback shape
 * as `useMoveTaskMutation`. `onSettled` still refetches so the server's own
 * `readAt` eventually replaces the optimistic guess.
 */
export function useMarkNotificationReadMutation(options: { onRefused?: (error: unknown) => void } = {}) {
  const queryClient = useQueryClient()

  return useMutation<unknown, unknown, number, MarkReadContext>({
    mutationFn: (id: number) => markNotificationRead(id),
    onMutate: async (id) => {
      await queryClient.cancelQueries({ queryKey: ['notifications'] })
      const previousList = queryClient.getQueryData<NotificationsData>(NOTIFICATIONS_QUERY_KEY)
      const previousUnreadCount = queryClient.getQueryData<number>(UNREAD_NOTIFICATION_COUNT_QUERY_KEY)
      const idSet = new Set([id])

      queryClient.setQueryData<NotificationsData>(NOTIFICATIONS_QUERY_KEY, (current) => withIdsMarkedRead(current, idSet))
      queryClient.setQueryData<number>(UNREAD_NOTIFICATION_COUNT_QUERY_KEY, (current) =>
        current == null ? current : Math.max(0, current - 1),
      )

      return { previousList, previousUnreadCount }
    },
    onError: (error, _id, context) => {
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
