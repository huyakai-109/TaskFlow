import { useEffect, useRef, useState } from 'react'

import { describeError } from '../../api/errorMessages'

import { ActivityFilterBar } from './components/ActivityFilterBar'
import type { ActivityFilterState } from './components/ActivityFilterBar'
import { ActivityRow } from './components/ActivityRow'
import { useActivityQuery } from './hooks/useActivityQuery'

const EMPTY_FILTERS: ActivityFilterState = { actor: '', from: '', to: '', events: [] }

/**
 * The read-only, cross-task feed (`docs/features/f04-activity-feed.md`) that
 * replaced the old sidebar "Inbox" slot — visible to every role (`ViewTasks`
 * is held by everyone), no counter badge. Incremental loading via keyset
 * cursor + `IntersectionObserver` (same pattern as `NotificationBell`'s
 * popover list, no interval polling — R9: reload on open, on refocus
 * (TanStack Query's own default `refetchOnWindowFocus`), or the explicit
 * Refresh button).
 */
export function ActivityPage() {
  const [filterState, setFilterState] = useState<ActivityFilterState>(EMPTY_FILTERS)

  const filters = {
    actor: filterState.actor === '' ? undefined : filterState.actor,
    from: filterState.from === '' ? undefined : filterState.from,
    to: filterState.to === '' ? undefined : filterState.to,
    events: filterState.events.length === 0 ? undefined : filterState.events,
  }

  const query = useActivityQuery(filters)
  const items = query.data?.pages.flatMap((page) => page.result ?? []) ?? []
  const hasFilters = filterState.actor !== '' || filterState.from !== '' || filterState.to !== '' || filterState.events.length > 0

  const listRef = useRef<HTMLUListElement>(null)
  const sentinelRef = useRef<HTMLLIElement>(null)
  const hasNextPage = query.hasNextPage === true
  const isFetchingNextPage = query.isFetchingNextPage
  const isFetchNextPageError = query.isFetchNextPageError
  const fetchNextPage = query.fetchNextPage

  useEffect(() => {
    if (!hasNextPage || isFetchNextPageError) {
      return
    }

    const sentinel = sentinelRef.current
    const root = listRef.current

    if (sentinel == null || root == null) {
      return
    }

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0]?.isIntersecting === true && !isFetchingNextPage) {
          void fetchNextPage()
        }
      },
      { root },
    )

    observer.observe(sentinel)
    return () => observer.disconnect()
  }, [hasNextPage, isFetchingNextPage, isFetchNextPageError, fetchNextPage, items.length])

  return (
    <section className="flex h-full min-h-0 flex-col gap-[var(--space-4)]">
      <h1 className="m-0 text-[length:var(--font-size-display)] font-semibold">Activity</h1>

      <ActivityFilterBar
        value={filterState}
        onChange={setFilterState}
        onRefresh={() => void query.refetch()}
        isRefreshing={query.isRefetching}
      />

      {query.isPending && (
        <p role="status" className="text-[length:var(--font-size-sm)] text-text-muted">
          Loading activity…
        </p>
      )}

      {query.isError && items.length === 0 && (
        <div role="alert" className="flex flex-col items-start gap-[var(--space-3)] rounded-md border border-danger-border bg-danger-surface p-[var(--space-4)] text-danger">
          <p className="m-0">{describeError(query.error)}</p>
          <button
            type="button"
            className="min-h-9 cursor-pointer rounded-md border border-border bg-surface px-[var(--space-3)] text-text"
            onClick={() => void query.refetch()}
          >
            Retry
          </button>
        </div>
      )}

      {query.isSuccess && items.length === 0 && (
        <p className="text-[length:var(--font-size-sm)] text-text-muted">
          {hasFilters ? 'No activity matches these filters.' : 'No activity yet.'}
          {hasFilters && (
            <button
              type="button"
              className="ml-[var(--space-2)] cursor-pointer border-0 bg-transparent text-accent-ink underline [font:inherit]"
              onClick={() => setFilterState(EMPTY_FILTERS)}
            >
              Clear filters
            </button>
          )}
        </p>
      )}

      {items.length > 0 && (
        <ul ref={listRef} className="m-0 min-h-0 flex-1 list-none overflow-y-auto p-0">
          {items.map((entry) => (
            <ActivityRow key={entry.id} entry={entry} />
          ))}

          {hasNextPage && !isFetchNextPageError && <li ref={sentinelRef} aria-hidden="true" className="h-px" />}

          {(isFetchingNextPage || isFetchNextPageError || !hasNextPage) && (
            <li aria-live="polite" className="flex justify-center p-[var(--space-3)] text-[length:var(--font-size-xs)] text-text-muted">
              {isFetchNextPageError ? (
                <button
                  type="button"
                  className="cursor-pointer rounded-sm border border-border bg-surface px-[var(--space-2)] py-[4px] text-[length:var(--font-size-xs)] text-text-muted hover:bg-accent-soft hover:text-accent-ink"
                  onClick={() => void fetchNextPage()}
                >
                  Couldn&rsquo;t load more — Retry
                </button>
              ) : isFetchingNextPage ? (
                'Loading more…'
              ) : (
                'No more activity'
              )}
            </li>
          )}
        </ul>
      )}
    </section>
  )
}
