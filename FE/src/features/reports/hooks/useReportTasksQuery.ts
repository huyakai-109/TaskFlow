import { useInfiniteQuery } from '@tanstack/react-query'

import { getReportTasks } from '../../../api/reports'
import type { PaginationResultRes, ReportTask, ReportTaskMetric } from '../../../api/types'

const REPORT_TASKS_PAGE_SIZE = 30

export interface UseReportTasksQueryParams {
  userId?: number
  from: string
  to: string
  metric: ReportTaskMetric
  /** Only fetch once the drill-down dialog is actually open. */
  enabled: boolean
}

/** `GET /reports/tasks` — offset paging (§11.7 "Show more", 30 per page), not keyset: a bounded, recomputed list, the house pattern for drill-downs (ADR 0007's own rule, reused by F09). */
export function useReportTasksQuery(params: UseReportTasksQueryParams) {
  return useInfiniteQuery<PaginationResultRes<ReportTask[]>>({
    queryKey: ['reports', 'tasks', params.userId ?? 'team', params.from, params.to, params.metric],
    queryFn: ({ pageParam, signal }) => {
      const pageNumber = pageParam as number
      const start = (pageNumber - 1) * REPORT_TASKS_PAGE_SIZE + 1
      return getReportTasks({ userId: params.userId, from: params.from, to: params.to, metric: params.metric }, start, REPORT_TASKS_PAGE_SIZE, signal)
    },
    initialPageParam: 1,
    getNextPageParam: (lastPage) => {
      const pagination = lastPage.pagination

      if (pagination == null || pagination.pageNumber >= pagination.totalPages) {
        return undefined
      }

      return pagination.pageNumber + 1
    },
    enabled: params.enabled,
  })
}
