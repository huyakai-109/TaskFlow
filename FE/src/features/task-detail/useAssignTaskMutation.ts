import { useMutation, useQueryClient } from '@tanstack/react-query'

import { ApiError } from '../../api/client'
import { assignTask } from '../../api/tasks'

interface Variables {
  taskId: number
  assigneeId: number | null
  version: string
}

const CONFLICT_CODES = new Set(['Task.Assign.Conflict', 'Task.Assign.NotFound'])

/** `PUT /tasks/{id}/assignee` — the assignee picker. `assigneeId: null` unassigns. */
export function useAssignTaskMutation(options: { onRefused: (code: string | null) => void }) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ taskId, assigneeId, version }: Variables) => assignTask(taskId, assigneeId, version),
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
