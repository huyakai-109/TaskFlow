import { useMutation, useQueryClient } from '@tanstack/react-query'

import { ApiError, refreshAccessToken } from '../../../api/client'
import { FORBIDDEN_SENTINEL } from '../../../api/errorMessages'
import { updateTaskPriority } from '../../../api/tasks'
import type { TaskPriority } from '../../../api/types'

interface Variables {
  taskId: number
  priority: TaskPriority
  version: string
}

const CONFLICT_CODES = new Set(['Task.UpdatePriority.Conflict', 'Task.UpdatePriority.NotFound'])

/**
 * `PUT /tasks/{id}/priority` — the properties panel's inline priority
 * control, policy `PlanTasks` (Revision R2). A 403 here carries no
 * `errorCode` (permission failures never do), and can mean the session's
 * `permissions` are stale — up to ~15 minutes after a role change
 * (`round-2-decisions.md` "Deploy note for R2-1"). On a 403, trigger one
 * session refresh (bounded — no retry loop, and the original request is
 * never retried automatically) so the control's own permission check picks
 * up the fresh value on the next render, and report `FORBIDDEN_SENTINEL`
 * so the caller shows a clear message through the same error-code table
 * instead of a raw 403.
 */
export function useUpdatePriorityMutation(options: { onRefused: (code: string | null) => void }) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ taskId, priority, version }: Variables) => updateTaskPriority(taskId, priority, version),
    onSuccess: (_task, variables) => {
      void queryClient.invalidateQueries({ queryKey: ['task', variables.taskId] })
      void queryClient.invalidateQueries({ queryKey: ['board'] })
    },
    onError: (error, variables) => {
      if (error instanceof ApiError && error.status === 403) {
        void refreshAccessToken().catch(() => {})
        options.onRefused(FORBIDDEN_SENTINEL)
        return
      }

      const code = error instanceof ApiError ? error.errorCode : null
      options.onRefused(code)

      if (code !== null && CONFLICT_CODES.has(code)) {
        void queryClient.invalidateQueries({ queryKey: ['task', variables.taskId] })
      }
    },
  })
}
