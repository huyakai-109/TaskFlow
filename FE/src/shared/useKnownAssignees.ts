import { useQueryClient } from '@tanstack/react-query'

import type { Assignee, Board } from '../api/types'

/**
 * `GET api/v1/users` is approved in the contract (`docs/ROADMAP.md` *Technical
 * contract sign-off* #6, for the assignee picker) but **no `UsersController`
 * exists on this branch** (verified: no controller file under
 * `BE/TaskFlow.Api/Controllers/V1/`, no `/users` route). Rather than inventing
 * a backend endpoint, the picker is built from the assignees already visible
 * in the board's cached data — everyone currently assigned to at least one
 * task, read straight out of the `['board']` query cache so this costs no
 * extra request.
 *
 * This is a deliberate stopgap, not a user directory: someone who has never
 * been assigned a task cannot be picked, and F02's capacity data does not
 * exist yet, so the list is sorted alphabetically instead of "most room
 * first" and nobody is shown as disabled (`docs/ui/screens/AssignPicker.dc.html`'s
 * sorting/disabling is F02 territory — not built, see `docs/ROADMAP.md` unit 3).
 */
export function useKnownAssignees(): Assignee[] {
  const queryClient = useQueryClient()
  const board = queryClient.getQueryData<Board>(['board'])

  const byId = new Map<number, Assignee>()

  for (const column of board?.columns ?? []) {
    for (const item of column.items) {
      if (item.assignee !== null) {
        byId.set(item.assignee.id, item.assignee)
      }
    }
  }

  return [...byId.values()].sort((a, b) => a.userName.localeCompare(b.userName))
}
