import { useQuery } from '@tanstack/react-query'

import { getBoard } from '../../api/tasks'

/** Moved to `shared/` (from `features/board/`) at its second use — `features/my-tasks` reads the same board data, and a feature may not import another feature (`frontend.md` import direction). */
export function useBoardQuery() {
  return useQuery({
    queryKey: ['board'],
    queryFn: ({ signal }) => getBoard(signal),
  })
}
