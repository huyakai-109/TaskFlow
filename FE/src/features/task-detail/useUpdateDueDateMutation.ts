import { useMutation, useQueryClient } from '@tanstack/react-query'

import { ApiError } from '../../api/client'
import { updateTaskDueDate } from '../../api/tasks'

interface Variables {
  taskId: number
  dueDate: string | null
  version: string
}

const CONFLICT_CODES = new Set(['Task.UpdateDueDate.Conflict', 'Task.UpdateDueDate.NotFound'])

/** `PUT /tasks/{id}/due-date` — the properties panel's inline due-date control. An explicit `null` clears it. */
export function useUpdateDueDateMutation(options: { onRefused: (code: string | null) => void }) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ taskId, dueDate, version }: Variables) => updateTaskDueDate(taskId, dueDate, version),
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
