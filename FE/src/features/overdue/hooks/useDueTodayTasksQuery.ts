import { useInfiniteQuery } from '@tanstack/react-query'

import { getNonOverdueTasks } from '../../../api/tasks'
import type { PaginationResultRes, TaskSummary } from '../../../api/types'

/** Own paginated fetch (not the board cache) — see `docs/design/COMING-SOON.md` A10's note not to inherit the board's 50/20 cap for something that doesn't need it. */
const PAGE_SIZE = 50

/**
 * "Due today — not overdue yet" (`docs/design/SPEC.md` §5.7, reopened
 * 2026-11-11; `docs/design/COMING-SOON.md` A10). There is no due-date
 * filter, so this walks `GET /tasks?isOverdue=false` the same paginated way
 * `useOverdueTasksQuery` walks `isOverdue=true`, and the caller filters the
 * loaded pages down to `dueDate` = today. Honest about incompleteness the
 * same way the existing Overdue list is: only pages actually fetched are
 * ever shown, with their own "Show more" — never a silent guess at what a
 * later, unfetched page might contain.
 */
export function useDueTodayTasksQuery() {
  return useInfiniteQuery<PaginationResultRes<TaskSummary[]>>({
    queryKey: ['board', 'due-today-candidates'],
    queryFn: ({ pageParam, signal }) => {
      const pageNumber = pageParam as number
      const start = (pageNumber - 1) * PAGE_SIZE + 1
      return getNonOverdueTasks(start, PAGE_SIZE, signal)
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
