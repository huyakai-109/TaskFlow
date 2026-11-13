import { useMutation, useQueryClient } from '@tanstack/react-query'

import { markNotificationRead } from '../../../api/notifications'
import type { PaginationResultRes, TaskNotification } from '../../../api/types'

interface MarkReadContext {
  previous: PaginationResultRes<TaskNotification[]> | undefined
}

/**
 * Marks one notification read. Optimistic: the bell's badge and that row's
 * read styling (`NotificationBell`) update immediately from the local cache
 * write below, not after the request round-trips — `onError` rolls the cache
 * back to its snapshot and reports the failure through `onRefused`, same
 * rollback shape as `useMoveTaskMutation`. `onSettled` still refetches so the
 * server's own `readAt` eventually replaces the optimistic guess.
 */
export function useMarkNotificationReadMutation(options: { onRefused?: (error: unknown) => void } = {}) {
  const queryClient = useQueryClient()

  return useMutation<unknown, unknown, number, MarkReadContext>({
    mutationFn: (id: number) => markNotificationRead(id),
    onMutate: async (id) => {
      await queryClient.cancelQueries({ queryKey: ['notifications'] })
      const previous = queryClient.getQueryData<PaginationResultRes<TaskNotification[]>>(['notifications'])

      queryClient.setQueryData<PaginationResultRes<TaskNotification[]>>(['notifications'], (current) => {
        if (current?.result == null) {
          return current
        }

        return {
          ...current,
          result: current.result.map((notification) =>
            notification.id === id
              ? { ...notification, isRead: true, readAt: notification.readAt ?? new Date().toISOString() }
              : notification,
          ),
        }
      })

      return { previous }
    },
    onError: (error, _id, context) => {
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
