import { useQuery } from '@tanstack/react-query'

import { getTaskHistory } from '../../api/tasks'

/** Oldest-first page of the task's history, per T5 endpoint 12; 100 is the API's max page size. */
export function useTaskHistoryQuery(taskId: number) {
  return useQuery({
    queryKey: ['task', taskId, 'history'],
    queryFn: async ({ signal }) => {
      const envelope = await getTaskHistory(taskId, 1, 100, signal)
      return envelope.result ?? []
    },
    enabled: Number.isFinite(taskId),
  })
}
