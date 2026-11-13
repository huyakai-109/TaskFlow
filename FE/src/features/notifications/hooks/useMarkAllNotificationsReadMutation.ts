import { useMutation, useQueryClient } from '@tanstack/react-query'

import { markNotificationRead } from '../../../api/notifications'

/**
 * "Mark all as read" (`NotificationBell.tsx`). There is no bulk endpoint
 * (`docs/features/f01-workflow.md` T14 is per-id only) and none is added
 * here (FE-only task) — this calls the existing idempotent `PUT
 * /notifications/{id}/read` once per currently-unread id.
 */
export function useMarkAllNotificationsReadMutation() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (unreadIds: number[]) => {
      await Promise.all(unreadIds.map((id) => markNotificationRead(id)))
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['notifications'] })
    },
  })
}
