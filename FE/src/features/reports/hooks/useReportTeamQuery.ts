import { useQuery } from '@tanstack/react-query'

import { getTeamReport } from '../../../api/reports'

export interface UseReportTeamQueryParams {
  from: string
  to: string
}

/** `GET /reports/team` — `ViewReports` only (403 otherwise, the server's own boundary; `RequirePermission` keeps this query from ever mounting for a Member). */
export function useReportTeamQuery(params: UseReportTeamQueryParams) {
  const ready = params.from !== '' && params.to !== ''

  return useQuery({
    queryKey: ['reports', 'team', params.from, params.to],
    queryFn: ({ signal }) => getTeamReport({ from: params.from, to: params.to }, signal),
    enabled: ready,
  })
}
