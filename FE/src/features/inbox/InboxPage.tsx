import { Link } from 'react-router-dom'

import { describeError } from '../../api/errorMessages'

import { useInboxQuery } from './hooks/useInboxQuery'
import { useMarkInboxReadMutation } from './hooks/useMarkInboxReadMutation'

const timestampFormatter = new Intl.DateTimeFormat('en', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })

/**
 * The Inbox page (`docs/design/COMING-SOON.md` A1, `docs/design/SPEC.md` §10
 * point 5, reopened 2026-11-11) — every notification, read and unread, from
 * the existing `GET /notifications`. F01 D7's only MVP event
 * (`TaskNotificationEvent = 'ReturnedForRework'`) is rendered the same way
 * the bell dropdown does (`NotificationBell.tsx`), just as a full page with
 * both read and unread rows instead of a short unread-only dropdown.
 */
export function InboxPage() {
  const query = useInboxQuery()
  const markReadMutation = useMarkInboxReadMutation()
  const pages = query.data?.pages ?? []
  const items = pages.flatMap((page) => page.result ?? [])
  const pagination = pages.at(-1)?.pagination ?? null
  const totalItems = pagination?.totalItems ?? items.length
  const hasMore = items.length < totalItems

  return (
    <section className="flex max-w-[720px] flex-col gap-[var(--space-4)]">
      <h1 className="m-0 text-[length:var(--font-size-xl)] font-semibold">Inbox</h1>

      {query.isPending && (
        <p role="status" className="text-[length:var(--font-size-sm)] text-text-muted">
          Loading inbox…
        </p>
      )}

      {query.isError && (
        <div
          className="flex flex-col items-start gap-[var(--space-3)] rounded-md border border-danger-border bg-danger-surface p-[var(--space-4)] text-danger"
          role="alert"
        >
          <p>{describeError(query.error)}</p>
          <button type="button" onClick={() => query.refetch()}>
            Try again
          </button>
        </div>
      )}

      {query.isSuccess && items.length === 0 && (
        <p className="text-[length:var(--font-size-sm)] text-text-muted">You&rsquo;re all caught up.</p>
      )}

      {query.isSuccess && items.length > 0 && (
        <ul className="m-0 flex list-none flex-col gap-[2px] rounded-lg border border-border bg-surface p-[var(--space-2)]">
          {items.map((notification) => (
            <li
              key={notification.id}
              className="flex items-center gap-[var(--space-3)] rounded-md px-[var(--space-3)] py-[var(--space-2)] hover:bg-column"
            >
              <span
                aria-hidden="true"
                className="size-2 shrink-0 rounded-pill"
                style={{ backgroundColor: notification.isRead ? 'transparent' : 'var(--color-accent)' }}
              />
              <Link
                to={`/tasks/${notification.taskId}`}
                className="grow text-[length:var(--font-size-sm)] text-text no-underline hover:underline"
              >
                Task #{notification.taskId} was sent back for rework
              </Link>
              <time className="shrink-0 text-[length:var(--font-size-xs)] text-text-muted">
                {timestampFormatter.format(new Date(notification.createdAt))}
              </time>
              {!notification.isRead && (
                <button
                  type="button"
                  className="shrink-0 cursor-pointer rounded-sm border border-border bg-surface px-[var(--space-2)] py-[4px] text-[length:var(--font-size-xs)] text-text-muted hover:bg-accent-soft hover:text-accent-ink"
                  onClick={() => markReadMutation.mutate(notification.id)}
                >
                  Mark read
                </button>
              )}
            </li>
          ))}
        </ul>
      )}

      {query.isSuccess && hasMore && (
        <button
          type="button"
          className="min-h-10 cursor-pointer self-start rounded-md border border-border bg-surface px-[var(--space-3)] text-[length:var(--font-size-sm)] font-medium text-text hover:border-accent hover:bg-accent-soft hover:text-accent-ink disabled:cursor-not-allowed disabled:opacity-60"
          onClick={() => query.fetchNextPage()}
          disabled={query.isFetchingNextPage}
        >
          {query.isFetchingNextPage ? 'Loading…' : `Show more (${items.length} of ${totalItems})`}
        </button>
      )}
    </section>
  )
}
