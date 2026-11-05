import { useMutation, useQueryClient } from '@tanstack/react-query'

import { ApiError } from '../../api/client'
import { moveTask } from '../../api/tasks'
import type { Board, TaskStatus, TaskSummary } from '../../api/types'

interface MoveVariables {
  taskId: number
  toStatus: TaskStatus
  version: string
  /** Carried through to `onRefused` so a dialog or toast can name the task. */
  taskTitle: string
}

interface MoveContext {
  previousBoard: Board | undefined
}

/**
 * The board's drag-and-drop move: optimistic update, rollback on failure,
 * then an unconditional refetch (`docs/features/f01-workflow.md` T2 — a
 * `Guid` version token means a `Task.Move.Conflict` leaves the dragged card's
 * `version` stale, so `onError`'s rollback alone is not enough; `onSettled`'s
 * refetch is what brings the board back to the server's truth whether the
 * move succeeded or not).
 */
export function useMoveTaskMutation(options: {
  onRefused: (code: string | null, variables: MoveVariables, message: string | null) => void
}) {
  const queryClient = useQueryClient()

  return useMutation<unknown, unknown, MoveVariables, MoveContext>({
    mutationFn: ({ taskId, toStatus, version }) => moveTask(taskId, toStatus, version),
    onMutate: async (variables) => {
      await queryClient.cancelQueries({ queryKey: ['board'] })
      const previousBoard = queryClient.getQueryData<Board>(['board'])

      queryClient.setQueryData<Board>(['board'], (current) => {
        if (current === undefined) {
          return current
        }

        let movedTask: TaskSummary | undefined

        const withoutTask = current.columns.map((column) => ({
          ...column,
          items: column.items.filter((item) => {
            if (item.id === variables.taskId) {
              movedTask = item
              return false
            }

            return true
          }),
        }))

        if (movedTask === undefined) {
          return current
        }

        const moved = { ...movedTask, status: variables.toStatus }

        return {
          columns: withoutTask.map((column) =>
            column.status === variables.toStatus ? { ...column, items: [...column.items, moved] } : column,
          ),
        }
      })

      return { previousBoard }
    },
    onError: (error, variables, context) => {
      if (context?.previousBoard !== undefined) {
        queryClient.setQueryData(['board'], context.previousBoard)
      }

      const code = error instanceof ApiError ? error.errorCode : null
      const message = error instanceof ApiError ? error.message : null
      options.onRefused(code, variables, message)
    },
    onSettled: (_data, _error, variables) => {
      void queryClient.invalidateQueries({ queryKey: ['board'] })
      void queryClient.invalidateQueries({ queryKey: ['task', variables.taskId] })
      void queryClient.invalidateQueries({ queryKey: ['task', variables.taskId, 'history'] })
    },
  })
}
