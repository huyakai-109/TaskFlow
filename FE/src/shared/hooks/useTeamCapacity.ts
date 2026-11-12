import { useMemo } from 'react'

import type { UserListItem } from '../../api/types'
import { useBoardQuery } from './useBoardQuery'
import { useUsersQuery } from './useUsersQuery'

/**
 * `docs/design/SPEC.md` §10 point 3 (reopened 2026-11-11): the per-user
 * in-progress count behind Team capacity and the Assign picker's "HAS ROOM" /
 * "AT CAPACITY" grouping. `GET /users` has no such field
 * (`docs/features/f02-capacity.md` T4 — `UserResDto` is id/userName/role
 * only), so this is a client-side reduction over the board's already-loaded
 * `InProgress` column, not a new endpoint.
 *
 * ASSUMPTION: the in-progress limit is 3 — the same number the design and
 * every existing error message (`errorMessages.ts`) show, but the backend's
 * is a configurable value (`ConfigKeys`, `CLAUDE.md`) no endpoint exposes to
 * the client. If it's ever changed from 3 in configuration, this display
 * goes stale until a field is added to carry it — flagged, not silently
 * assumed correct.
 *
 * Inherits the board's own pagination limit (`BoardColumnPageSize`, 50): a
 * person whose in-progress tasks aren't on the board's loaded page is
 * undercounted here too (same documented limitation as My tasks, SPEC §10
 * point 9).
 */
export const IN_PROGRESS_LIMIT = 3

export interface TeamCapacityEntry {
  user: UserListItem
  inProgressCount: number
  isFull: boolean
}

export interface UseTeamCapacityResult {
  isPending: boolean
  isError: boolean
  /** Full first, then most-loaded first, then alphabetical — mirrors §5.2's ordering. */
  entries: TeamCapacityEntry[]
  fullCount: number
  almostFullCount: number
  /** `InProgress` has more pages than loaded (`BoardColumnPageSize`) — counts above may undercount. Same pattern as My Tasks's own "may not be complete" note. */
  mayBeIncomplete: boolean
}

export function useTeamCapacity(): UseTeamCapacityResult {
  const usersQuery = useUsersQuery()
  const boardQuery = useBoardQuery()

  const entries = useMemo<TeamCapacityEntry[]>(() => {
    const users = usersQuery.data ?? []
    const inProgressItems = boardQuery.data?.columns.find((c) => c.status === 'InProgress')?.items ?? []

    const counts = new Map<number, number>()
    for (const task of inProgressItems) {
      if (task.assignee !== null) {
        counts.set(task.assignee.id, (counts.get(task.assignee.id) ?? 0) + 1)
      }
    }

    return users
      .map((user) => {
        const inProgressCount = counts.get(user.id) ?? 0
        return { user, inProgressCount, isFull: inProgressCount >= IN_PROGRESS_LIMIT }
      })
      .sort((a, b) => {
        if (a.isFull !== b.isFull) {
          return a.isFull ? -1 : 1
        }

        if (a.inProgressCount !== b.inProgressCount) {
          return b.inProgressCount - a.inProgressCount
        }

        return a.user.userName.localeCompare(b.user.userName)
      })
  }, [usersQuery.data, boardQuery.data])

  const inProgressColumn = boardQuery.data?.columns.find((c) => c.status === 'InProgress')
  const inProgressPagination = inProgressColumn?.pagination ?? null
  const mayBeIncomplete =
    inProgressPagination !== null && (inProgressColumn?.items.length ?? 0) < inProgressPagination.totalItems

  return {
    isPending: usersQuery.isPending || boardQuery.isPending,
    isError: usersQuery.isError || boardQuery.isError,
    entries,
    fullCount: entries.filter((e) => e.isFull).length,
    almostFullCount: entries.filter((e) => !e.isFull && e.inProgressCount === IN_PROGRESS_LIMIT - 1).length,
    mayBeIncomplete,
  }
}
