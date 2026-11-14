import { useMutation, useQueryClient } from '@tanstack/react-query'

import { ApiError } from '../../api/client'
import { assignTask } from '../../api/tasks'

interface Variables {
  taskId: number
  assigneeId: number | null
  version: string
}

const CONFLICT_CODES = new Set(['Task.Assign.Conflict', 'Task.Assign.NotFound'])

/**
 * `PUT /tasks/{id}/assignee` — the assignee picker (task detail) and the
 * "Take over" action (task detail and the board card's menu). `assigneeId:
 * null` unassigns. Promoted from `features/task-detail/hooks/` to `shared/`
 * at its second use (`FE/src/features/board/BoardPage.tsx`'s "Take over"):
 * `shared/` may not be reached into by two features independently
 * (`frontend.md` layering) — same precedent `CardMoveAction`'s own doc
 * comment already set for its move to `shared/components/`.
 */
export function useAssignTaskMutation(options: { onRefused: (code: string | null, message: string | null) => void }) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ taskId, assigneeId, version }: Variables) => assignTask(taskId, assigneeId, version),
    onSuccess: (_task, variables) => {
      void queryClient.invalidateQueries({ queryKey: ['task', variables.taskId] })
      void queryClient.invalidateQueries({ queryKey: ['board'] })
    },
    onError: (error, variables) => {
      const code = error instanceof ApiError ? error.errorCode : null
      const message = error instanceof ApiError ? error.message : null
      options.onRefused(code, message)

      if (code !== null && CONFLICT_CODES.has(code)) {
        void queryClient.invalidateQueries({ queryKey: ['task', variables.taskId] })
      }
    },
  })
}
