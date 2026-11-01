import { useMutation, useQueryClient } from '@tanstack/react-query'

import { ApiError } from '../../api/client'
import { updateTask } from '../../api/tasks'

interface UpdateVariables {
  taskId: number
  title: string
  description: string | null
  version: string
}

const CONFLICT_CODES = new Set(['Task.Update.Conflict', 'Task.Update.NotFound'])

/**
 * `PUT /tasks/{id}` — title/description only, no history entry (F04 scope).
 * Same shape as `useReopenTaskMutation`: a deliberate dialog submission, so a
 * plain round trip (no optimistic update) keeps rollback simple.
 */
export function useUpdateTaskMutation(options: { onRefused: (code: string | null) => void }) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ taskId, title, description, version }: UpdateVariables) =>
      updateTask(taskId, title, description, version),
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
