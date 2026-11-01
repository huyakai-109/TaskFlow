import { useQuery } from '@tanstack/react-query'

import { getTask } from '../../api/tasks'

export function useTaskQuery(taskId: number) {
  return useQuery({
    queryKey: ['task', taskId],
    queryFn: ({ signal }) => getTask(taskId, signal),
    enabled: Number.isFinite(taskId),
  })
}
