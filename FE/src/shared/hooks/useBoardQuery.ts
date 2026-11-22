import { useQuery } from '@tanstack/react-query'

import { getBoard } from '../../api/tasks'
import type { BoardSort } from '../../api/types'

/**
 * Moved to `shared/` (from `features/board/`) at its second use — `features/my-tasks` reads the same board data, and a feature may not import another feature (`frontend.md` import direction).
 *
 * `sort` (f10-read-models-and-sort.md §1) is part of the query key: switching
 * it is a different server order, so it must be a different cache entry, not
 * a client-side re-sort of whatever happens to be cached under `priority`.
 */
export function useBoardQuery(sort: BoardSort = 'priority') {
  return useQuery({
    queryKey: ['board', sort],
    queryFn: ({ signal }) => getBoard(sort, signal),
  })
}
