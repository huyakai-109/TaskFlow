import { useQuery } from '@tanstack/react-query'

import { getWorkflow } from '../../api/workflow'

/**
 * `GET /workflow` — the real configured in-progress limit (f10 §4), replacing
 * the "max —" `ComingSoon` stub (`docs/design/COMING-SOON.md` B4) that used
 * to sit here before this endpoint existed. Rarely changes, so a long
 * `staleTime` is reasonable — still a real fetch, never a hardcoded literal.
 */
export function useWorkflowQuery() {
  return useQuery({
    queryKey: ['workflow'],
    queryFn: ({ signal }) => getWorkflow(signal),
    staleTime: 5 * 60 * 1000,
  })
}
