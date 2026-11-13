import { useMutation, useQueryClient } from '@tanstack/react-query'

import { markNotificationRead } from '../../../api/notifications'
import type { PaginationResultRes, TaskNotification } from '../../../api/types'

interface MarkAllReadContext {
  previous: PaginationResultRes<TaskNotification[]> | undefined
}

/**
 * "Mark all as read" (`NotificationBell.tsx`). There is no bulk endpoint
 * (`docs/features/f01-workflow.md` T14 is per-id only; a real bulk
 * mark-all-read endpoint is needed server-side — `docs/design/COMING-SOON.md`
 * B8) — this calls the existing idempotent `PUT /notifications/{id}/read`
 * once per currently-unread id, same stopgap as before. The cache write is
 * now optimistic and batched into the single `setQueryData` call below
 * (not one write per settled request), and the caller only ever passes ids
 * that are still unread, so no id already read gets a redundant request.
 * Rolls back to the pre-mutate snapshot on any failure, same shape as the
 * single mark-read mutation.
 */
export function useMarkAllNotificationsReadMutation(options: { onRefused?: (error: unknown) => void } = {}) {
  const queryClient = useQueryClient()

  return useMutation<unknown, unknown, number[], MarkAllReadContext>({
    mutationFn: async (unreadIds: number[]) => {
      await Promise.all(unreadIds.map((id) => markNotificationRead(id)))
    },
    onMutate: async (unreadIds) => {
      await queryClient.cancelQueries({ queryKey: ['notifications'] })
      const previous = queryClient.getQueryData<PaginationResultRes<TaskNotification[]>>(['notifications'])
      const idSet = new Set(unreadIds)

      queryClient.setQueryData<PaginationResultRes<TaskNotification[]>>(['notifications'], (current) => {
        if (current?.result == null) {
          return current
        }

        return {
          ...current,
          result: current.result.map((notification) =>
            idSet.has(notification.id)
              ? { ...notification, isRead: true, readAt: notification.readAt ?? new Date().toISOString() }
              : notification,
          ),
        }
      })

      return { previous }
    },
    onError: (error, _unreadIds, context) => {
      if (context?.previous !== undefined) {
        queryClient.setQueryData(['notifications'], context.previous)
      }

      options.onRefused?.(error)
    },
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: ['notifications'] })
    },
  })
}
