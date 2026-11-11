import { useQuery } from '@tanstack/react-query'

import { getTaskHistory } from '../../../api/tasks'

/**
 * `docs/design/SPEC.md` §5.8 "Sent back by `<name>` for rework"
 * (`docs/design/COMING-SOON.md` A11, built this session). Reads the task's
 * own history for the latest `StatusChanged` entry with `fromValue:
 * "Review"`/`toValue: "InProgress"` — the same rework transition F01 D7's
 * notification is raised for. Mirrors `features/board/hooks/useApprovedBy.ts`
 * (a different transition, kept local to each feature rather than shared —
 * `frontend.md` §1: a feature never imports another feature).
 */
export function useSentBackBy(taskId: number, enabled: boolean) {
  return useQuery({
    queryKey: ['task-history-sent-back-by', taskId],
    queryFn: async ({ signal }) => {
      const page = await getTaskHistory(taskId, 1, 100, signal)
      const entries = page.result ?? []
      const rework = [...entries].reverse().find((entry) => entry.event === 'StatusChanged' && entry.fromValue === 'Review' && entry.toValue === 'InProgress')
      return rework ?? null
    },
    enabled,
  })
}
