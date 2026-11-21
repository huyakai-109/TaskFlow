import { request } from './client'
import type { ActivityEntry, CursorResultRes, TaskHistoryEvent } from './types'

export interface ActivityFilters {
  /** A user id as a string, or the literal `'system'` for null-actor (System) entries. */
  actor?: string
  from?: string
  to?: string
  events?: TaskHistoryEvent[]
}

/**
 * `GET /activity` (`docs/features/f04-activity-feed.md` "Technical contract")
 * — keyset paging (ADR 0007): `cursor` is opaque, absent means the first
 * page. Filters combine with AND; an unset filter is no restriction.
 */
export async function getActivity(
  filters: ActivityFilters,
  cursor: string | undefined,
  length: number,
  signal?: AbortSignal,
): Promise<CursorResultRes<ActivityEntry[]>> {
  const params = new URLSearchParams({ length: String(length) })

  if (cursor !== undefined) {
    params.set('cursor', cursor)
  }

  if (filters.actor !== undefined) {
    params.set('actor', filters.actor)
  }

  if (filters.from !== undefined) {
    params.set('from', filters.from)
  }

  if (filters.to !== undefined) {
    params.set('to', filters.to)
  }

  for (const event of filters.events ?? []) {
    params.append('events', event)
  }

  return request<CursorResultRes<ActivityEntry[]>>(`/activity?${params.toString()}`, { signal })
}
