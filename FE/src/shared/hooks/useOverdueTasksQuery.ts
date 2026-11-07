import { useInfiniteQuery } from '@tanstack/react-query'

import { getOverdueTasks } from '../../api/tasks'
import type { PaginationResultRes, TaskSummary } from '../../api/types'

/** `length` sent explicitly on every page, per F05 T9 — not a board column size, so it gets its own constant. */
const OVERDUE_PAGE_SIZE = 50

/**
 * "Overdue now" (F05 T9), server-ordered (priority, then earliest due date —
 * unchanged from F03). Keyed under the `['board']` prefix, not `['overdue']`,
 * so every existing `invalidateQueries({ queryKey: ['board'] })` call (move,
 * create, priority, due date, assign, reopen) also refreshes this view —
 * the same reasoning as the board's own `useColumnPagesQuery`. Lives in
 * `shared/` because both `features/board` (the "N overdue" chip) and
 * `features/overdue` (the list) read it, and `shared/` may depend only on
 * `api/`.
 */
export function useOverdueTasksQuery() {
  return useInfiniteQuery<PaginationResultRes<TaskSummary[]>>({
    queryKey: ['board', 'overdue'],
    queryFn: ({ pageParam, signal }) => {
      const pageNumber = pageParam as number
      const start = (pageNumber - 1) * OVERDUE_PAGE_SIZE + 1
      return getOverdueTasks(start, OVERDUE_PAGE_SIZE, signal)
    },
    initialPageParam: 1,
    getNextPageParam: (lastPage) => {
      const pagination = lastPage.pagination

      if (pagination == null || pagination.pageNumber >= pagination.totalPages) {
        return undefined
      }

      return pagination.pageNumber + 1
    },
  })
}
