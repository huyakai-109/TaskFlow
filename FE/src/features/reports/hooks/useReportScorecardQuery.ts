import { useQuery } from '@tanstack/react-query'

import { getScorecard } from '../../../api/reports'
import type { ReportGranularity } from '../../../api/types'

export interface UseReportScorecardQueryParams {
  userId?: number
  from: string
  to: string
  granularity: ReportGranularity
}

/** `GET /reports/scorecard` — team scope when `userId` is absent (R1, enforced server-side). */
export function useReportScorecardQuery(params: UseReportScorecardQueryParams) {
  const ready = params.from !== '' && params.to !== ''

  return useQuery({
    queryKey: ['reports', 'scorecard', params.userId ?? 'team', params.from, params.to, params.granularity],
    queryFn: ({ signal }) =>
      getScorecard({ userId: params.userId, from: params.from, to: params.to, granularity: params.granularity }, signal),
    enabled: ready,
  })
}
