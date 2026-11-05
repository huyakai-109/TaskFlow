import { useMutation, useQueryClient } from '@tanstack/react-query'

import { ApiError } from '../../api/client'
import { assignTask, moveTask } from '../../api/tasks'
import type { TaskDetail, TaskStatus } from '../../api/types'

interface Variables {
  taskId: number
  toStatus: TaskStatus
  assigneeId: number
  version: string
}

export type AssignAndMoveStep = 'assign' | 'move'

export interface AssignAndMoveFailure {
  step: AssignAndMoveStep
  code: string | null
  message: string | null
}

interface StepFailure {
  step: AssignAndMoveStep
  error: unknown
}

function isStepFailure(value: unknown): value is StepFailure {
  return typeof value === 'object' && value !== null && 'step' in value
}

/**
 * `Task.Move.AssigneeRequired`'s way out: "Assign to … and start" does both
 * in one click (`docs/ui.md` line 90 — "the client may chain both calls
 * behind one button; if the second fails, say so — the assignment stays").
 * Chains `PUT …/assignee` then `PUT …/status` using the version the assign
 * step returns, not the stale one the failed move carried — `assignTask`'s
 * own result is the freshest token available without a second fetch.
 *
 * Reports which step failed so the caller can tell "pick again" (assign
 * itself was refused) from "already assigned, the move didn't go through"
 * (assign succeeded, move failed — e.g. the limit filled in the meantime).
 */
export function useAssignAndMoveTaskMutation(options: { onRefused: (failure: AssignAndMoveFailure) => void }) {
  const queryClient = useQueryClient()

  return useMutation<TaskDetail, StepFailure, Variables>({
    mutationFn: async ({ taskId, toStatus, assigneeId, version }) => {
      let assigned: TaskDetail

      try {
        assigned = await assignTask(taskId, assigneeId, version)
      } catch (error) {
        throw { step: 'assign', error } satisfies StepFailure
      }

      try {
        return await moveTask(taskId, toStatus, assigned.version)
      } catch (error) {
        throw { step: 'move', error } satisfies StepFailure
      }
    },
    onSuccess: (_task, variables) => {
      void queryClient.invalidateQueries({ queryKey: ['board'] })
      void queryClient.invalidateQueries({ queryKey: ['task', variables.taskId] })
      void queryClient.invalidateQueries({ queryKey: ['task', variables.taskId, 'history'] })
    },
    onError: (thrown, variables) => {
      // Either step may have changed server state (the assign can have
      // succeeded even when the overall mutation fails) — always resync.
      void queryClient.invalidateQueries({ queryKey: ['board'] })
      void queryClient.invalidateQueries({ queryKey: ['task', variables.taskId] })

      const failure = isStepFailure(thrown) ? thrown : { step: 'assign' as const, error: thrown }
      const code = failure.error instanceof ApiError ? failure.error.errorCode : null
      const message = failure.error instanceof ApiError ? failure.error.message : null

      options.onRefused({ step: failure.step, code, message })
    },
  })
}
