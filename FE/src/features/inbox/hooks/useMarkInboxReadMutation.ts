import { useMutation, useQueryClient } from '@tanstack/react-query'

import { markNotificationRead } from '../../../api/notifications'

/** Mirrors `features/notifications/hooks/useMarkNotificationReadMutation.ts` — kept local, see `useInboxQuery.ts`. */
export function useMarkInboxReadMutation() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: number) => markNotificationRead(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['inbox'] })
      void queryClient.invalidateQueries({ queryKey: ['notifications'] })
    },
  })
}
