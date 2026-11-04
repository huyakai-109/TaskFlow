import { useMutation, useQueryClient } from '@tanstack/react-query'

import type { CreateTaskInput } from '../../../api/tasks'
import { createTask } from '../../../api/tasks'

/**
 * `POST /tasks`. No optimistic update: there is no existing board card to
 * update against, and the new card's id/`version` only exist once the server
 * answers, so a plain pending/error/success flow is the right fit here.
 */
export function useCreateTaskMutation() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (input: CreateTaskInput) => createTask(input),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['board'] })
    },
  })
}
