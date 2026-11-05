import { request } from './client'
import type { ResultRes, UserListItem } from './types'

/**
 * `GET api/v1/users` (`docs/features/f02-capacity.md` T4, slice 3.3) — the
 * assignee picker's roster: everyone, not only people already visible on the
 * board (`useKnownAssignees`'s stopgap, slice 1.7, retired by this slice).
 */
export async function getUsers(signal?: AbortSignal): Promise<UserListItem[]> {
  const envelope = await request<ResultRes<UserListItem[]>>('/users', { signal })
  return envelope.result ?? []
}
