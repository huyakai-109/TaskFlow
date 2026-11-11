import { useQuery } from '@tanstack/react-query'

import { getTaskHistory } from '../../../api/tasks'

/**
 * `docs/design/SPEC.md` §4 TaskCard "Approved by Minh · 16 Oct"
 * (`docs/design/COMING-SOON.md` A12, built this session). Reads the task's
 * own history (`GET /tasks/{id}/history`) for the latest `StatusChanged`
 * entry whose `toValue` is `"Done"` — the actor who moved it there. Only
 * enabled for a Done card, so this never runs for the other four columns.
 */
export function useApprovedBy(taskId: number, enabled: boolean) {
  return useQuery({
    queryKey: ['task-history-approved-by', taskId],
    queryFn: async ({ signal }) => {
      const page = await getTaskHistory(taskId, 1, 100, signal)
      const entries = page.result ?? []
      const approval = [...entries].reverse().find((entry) => entry.event === 'StatusChanged' && entry.toValue === 'Done')
      return approval ?? null
    },
    enabled,
  })
}
