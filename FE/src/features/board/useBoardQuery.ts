import { useQuery } from '@tanstack/react-query'

import { getBoard } from '../../api/tasks'

export function useBoardQuery() {
  return useQuery({
    queryKey: ['board'],
    queryFn: ({ signal }) => getBoard(signal),
  })
}
