import { useInfiniteQuery } from '@tanstack/react-query'

import { getActivity } from '../../../api/activity'
import type { ActivityFilters } from '../../../api/activity'

export const ACTIVITY_PAGE_SIZE = 30

/**
 * `GET /activity`'s cursor (keyset) paging (ADR 0007) — a different
 * `useInfiniteQuery` shape from every other list in this app
 * (`useNotificationsQuery`/`useColumnPagesQuery`): `initialPageParam` is
 * `undefined` (no cursor = first page) and `getNextPageParam` reads the
 * server's own opaque `nextCursor`/`hasMore`, never a page number — there is
 * no "page 2" to compute, only "continue from this cursor." The whole
 * `filters` object is part of the query key so changing any filter starts a
 * fresh paging sequence instead of mixing cursors from two different filter
 * sets (R9: reload on open/refocus is TanStack Query's own default
 * `refetchOnWindowFocus`, no interval polling added).
 */
export function useActivityQuery(filters: ActivityFilters) {
  return useInfiniteQuery({
    queryKey: ['activity', filters],
    queryFn: ({ pageParam, signal }) => getActivity(filters, pageParam as string | undefined, ACTIVITY_PAGE_SIZE, signal),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => (lastPage.page?.hasMore === true ? (lastPage.page.nextCursor ?? undefined) : undefined),
  })
}
