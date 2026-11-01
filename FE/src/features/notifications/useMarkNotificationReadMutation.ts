import { useMutation, useQueryClient } from '@tanstack/react-query'

import { markNotificationRead } from '../../api/notifications'

export function useMarkNotificationReadMutation() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: number) => markNotificationRead(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['notifications'] })
    },
  })
}
