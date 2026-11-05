import { useInfiniteQuery } from '@tanstack/react-query'

import { getTaskPage } from '../../../api/tasks'
import type { PaginationResultRes, TaskStatus, TaskSummary } from '../../../api/types'

/**
 * Page 2+ of one board column (F03 "show more"). Keyed under the `['board']`
 * prefix so every existing `invalidateQueries({ queryKey: ['board'] })` call
 * (move, create, priority, due date, assign, reopen) also refetches whatever
 * extra pages are already loaded here — no mutation hook changes needed.
 *
 * `pageSize` must be the board's own `pagination.pageSize` for this column
 * (50 for the active columns, 20 for `Done`) so a fetched page continues
 * exactly where the board's page 1 ended (AC15) — never a literal.
 */
export function useColumnPagesQuery(status: TaskStatus, pageSize: number | undefined) {
  return useInfiniteQuery<PaginationResultRes<TaskSummary[]>>({
    queryKey: ['board', 'column', status],
    queryFn: ({ pageParam, signal }) => {
      const pageNumber = pageParam as number
      const start = (pageNumber - 1) * (pageSize ?? 0) + 1
      return getTaskPage(status, start, pageSize ?? 0, signal)
    },
    initialPageParam: 2,
    getNextPageParam: (lastPage) => {
      const pagination = lastPage.pagination

      if (pagination == null || pagination.pageNumber >= pagination.totalPages) {
        return undefined
      }

      return pagination.pageNumber + 1
    },
    enabled: false,
  })
}
