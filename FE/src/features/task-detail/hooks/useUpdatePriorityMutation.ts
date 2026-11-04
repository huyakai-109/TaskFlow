import { useMutation, useQueryClient } from '@tanstack/react-query'

import { ApiError } from '../../../api/client'
import { updateTaskPriority } from '../../../api/tasks'
import type { TaskPriority } from '../../../api/types'

interface Variables {
  taskId: number
  priority: TaskPriority
  version: string
}

const CONFLICT_CODES = new Set(['Task.UpdatePriority.Conflict', 'Task.UpdatePriority.NotFound'])

/** `PUT /tasks/{id}/priority` — the properties panel's inline priority control. */
export function useUpdatePriorityMutation(options: { onRefused: (code: string | null) => void }) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ taskId, priority, version }: Variables) => updateTaskPriority(taskId, priority, version),
    onSuccess: (_task, variables) => {
      void queryClient.invalidateQueries({ queryKey: ['task', variables.taskId] })
      void queryClient.invalidateQueries({ queryKey: ['board'] })
    },
    onError: (error, variables) => {
      const code = error instanceof ApiError ? error.errorCode : null
      options.onRefused(code)

      if (code !== null && CONFLICT_CODES.has(code)) {
        void queryClient.invalidateQueries({ queryKey: ['task', variables.taskId] })
      }
    },
  })
}
