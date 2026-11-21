import type { InfiniteData } from '@tanstack/react-query'
import { useMutation, useQueryClient } from '@tanstack/react-query'

import { markAllNotificationsRead } from '../../../api/notifications'
import type { PaginationResultRes, TaskNotification } from '../../../api/types'
import { NOTIFICATIONS_QUERY_KEY } from './useNotificationsQuery'
import { UNREAD_NOTIFICATION_COUNT_QUERY_KEY } from './useUnreadNotificationCountQuery'

type NotificationsData = InfiniteData<PaginationResultRes<TaskNotification[]>>

interface MarkAllReadContext {
  previousList: NotificationsData | undefined
  previousUnreadCount: number | undefined
}

function withAllMarkedRead(data: NotificationsData | undefined, upToId: number | undefined): NotificationsData | undefined {
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
          !notification.isRead && (upToId === undefined || notification.id <= upToId)
            ? { ...notification, isRead: true, readAt: notification.readAt ?? new Date().toISOString() }
            : notification,
        ),
      }
    }),
  }
}

/**
 * "Mark all as read" (`NotificationBell.tsx`). Calls the real bulk endpoint
 * (F06 "API", `PUT /notifications/read-all`) **once**, never one `PUT` per
 * id — the earlier stopgap (`Promise.all` over the per-id endpoint,
 * `docs/design/COMING-SOON.md` B8) is retired now that the endpoint exists.
 *
 * `upToId` is the newest id the caller has loaded (the first row of the
 * newest-first popover list) — the same "newest the user has seen" the
 * server itself keys off, so a notification that arrives while the popover
 * is open stays unread (F06's own `upToId` doc comment). Every unread row
 * with a smaller id, loaded or not, is covered by the same request.
 *
 * The cache write is optimistic (one `setQueryData` per cache, not per
 * request) and rolls back both the paged list and the unread-count cache on
 * failure, same shape as the single mark-read mutation.
 */
export function useMarkAllNotificationsReadMutation(options: { onRefused?: (error: unknown) => void } = {}) {
  const queryClient = useQueryClient()

  return useMutation<number, unknown, number | undefined, MarkAllReadContext>({
    mutationFn: (upToId: number | undefined) => markAllNotificationsRead(upToId),
    onMutate: async (upToId) => {
      await queryClient.cancelQueries({ queryKey: ['notifications'] })
      const previousList = queryClient.getQueryData<NotificationsData>(NOTIFICATIONS_QUERY_KEY)
      const previousUnreadCount = queryClient.getQueryData<number>(UNREAD_NOTIFICATION_COUNT_QUERY_KEY)

      queryClient.setQueryData<NotificationsData>(NOTIFICATIONS_QUERY_KEY, (current) => withAllMarkedRead(current, upToId))
      queryClient.setQueryData<number>(UNREAD_NOTIFICATION_COUNT_QUERY_KEY, () => 0)

      return { previousList, previousUnreadCount }
    },
    onError: (error, _upToId, context) => {
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
