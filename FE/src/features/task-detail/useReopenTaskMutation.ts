import { useMutation, useQueryClient } from '@tanstack/react-query'

import { ApiError } from '../../api/client'
import { reopenTask } from '../../api/tasks'

interface ReopenVariables {
  taskId: number
  reason: string
  version: string
}

/**
 * `Done → Review` with a required reason (F01 D6), Manager/Admin only. No
 * optimistic update: unlike a drag, this is a deliberate dialog submission,
 * so round-tripping once is an acceptable wait and keeps rollback simple.
 */
export function useReopenTaskMutation(options: { onRefused: (code: string | null) => void }) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ taskId, reason, version }: ReopenVariables) => reopenTask(taskId, reason, version),
    onSuccess: (_task, variables) => {
      void queryClient.invalidateQueries({ queryKey: ['task', variables.taskId] })
      void queryClient.invalidateQueries({ queryKey: ['task', variables.taskId, 'history'] })
      void queryClient.invalidateQueries({ queryKey: ['board'] })
    },
    onError: (error, variables) => {
      const code = error instanceof ApiError ? error.errorCode : null
      options.onRefused(code)

      if (code === 'Task.Reopen.Conflict' || code === 'Task.Reopen.NotFound') {
        void queryClient.invalidateQueries({ queryKey: ['task', variables.taskId] })
      }
    },
  })
}
