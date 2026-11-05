import { useQuery } from '@tanstack/react-query'

import { getUsers } from '../../api/users'

/**
 * `GET /users` — the roster behind every assignee picker (`docs/features/f02-capacity.md`
 * T4). Replaces `useKnownAssignees`'s board-derived stopgap now that the
 * endpoint exists (slice 3.3): everyone is listed, not only people already
 * assigned to a visible task.
 */
export function useUsersQuery() {
  return useQuery({
    queryKey: ['users'],
    queryFn: ({ signal }) => getUsers(signal),
  })
}
