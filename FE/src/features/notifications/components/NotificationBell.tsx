import clsx from 'clsx'
import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'

import { describeError } from '../../../api/errorMessages'
import { Avatar } from '../../../shared/components/Avatar'
import { MoveToast } from '../../../shared/components/MoveToast'
import { useUsersQuery } from '../../../shared/hooks/useUsersQuery'
import { useMarkAllNotificationsReadMutation } from '../hooks/useMarkAllNotificationsReadMutation'
import { useMarkNotificationReadMutation } from '../hooks/useMarkNotificationReadMutation'
import { useNotificationsQuery } from '../hooks/useNotificationsQuery'
import { useUnreadNotificationCountQuery } from '../hooks/useUnreadNotificationCountQuery'

const relativeTimeFormatter = new Intl.RelativeTimeFormat('en', { numeric: 'always' })
const RELATIVE_TIME_UNITS: ReadonlyArray<{ unit: Intl.RelativeTimeFormatUnit; ms: number }> = [
  { unit: 'year', ms: 365 * 24 * 60 * 60 * 1000 },
  { unit: 'month', ms: 30 * 24 * 60 * 60 * 1000 },
  { unit: 'week', ms: 7 * 24 * 60 * 60 * 1000 },
  { unit: 'day', ms: 24 * 60 * 60 * 1000 },
  { unit: 'hour', ms: 60 * 60 * 1000 },
  { unit: 'minute', ms: 60 * 1000 },
]

/** "5 hours ago" instead of an absolute date/time — a gap, not a clock reading. */
function formatRelativeTime(iso: string): string {
  const diffMs = new Date(iso).getTime() - Date.now()

  if (Math.abs(diffMs) < 60_000) {
    return 'Just now'
  }

  for (const { unit, ms } of RELATIVE_TIME_UNITS) {
    if (Math.abs(diffMs) >= ms) {
      return relativeTimeFormatter.format(Math.round(diffMs / ms), unit)
    }
  }

  return relativeTimeFormatter.format(Math.round(diffMs / 60_000), 'minute')
}

function BellIcon() {
  return (
    <svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
      <path d="M18 8a6 6 0 1 0-12 0c0 7-3 9-3 9h18s-3-2-3-9" />
      <path d="M13.73 21a2 2 0 0 1-3.46 0" />
    </svg>
  )
}

/**
 * The in-app inbox for F01 D7's one notification event (rework). No wireframe
 * draws this (`docs/design/SPEC.md` "Not designed yet" lists "notification inbox"), so
 * this is a minimal bell + unread badge + dropdown list, following the app's
 * existing popover visual language (raised surface, `--radius-xl`,
 * `--shadow-raised`) rather than inventing a new one.
 *
 * Shows both read and unread rows (`useNotificationsQuery`, fixed earlier
 * — a read notification used to disappear from the list entirely, a
 * client-side filter bug, not an API limitation; see that hook's own doc
 * comment). The list pages itself as the user scrolls (`IntersectionObserver`
 * below); the unread badge is `useUnreadNotificationCountQuery`'s own real
 * total, never derived from however many pages are loaded.
 */
export function NotificationBell() {
  const [open, setOpen] = useState(false)
  const [toastMessage, setToastMessage] = useState<string | null>(null)
  const notificationsQuery = useNotificationsQuery()
  const unreadCountQuery = useUnreadNotificationCountQuery()
  const usersQuery = useUsersQuery()
  const markReadMutation = useMarkNotificationReadMutation({
    onRefused: (error) => setToastMessage(describeError(error)),
  })
  const markAllReadMutation = useMarkAllNotificationsReadMutation({
    onRefused: (error) => setToastMessage(describeError(error)),
  })
  const pages = notificationsQuery.data?.pages ?? []
  const items = pages.flatMap((page) => page.result ?? [])
  const loadedUnreadIds = items.filter((notification) => !notification.isRead).map((notification) => notification.id)
  const lastPagination = pages.at(-1)?.pagination ?? null
  // The badge is the server's real unread total (F06 D4), never how many
  // unread rows happen to be loaded into the popover — falls back to the
  // loaded count only until that separate query has resolved once.
  const unreadCount = unreadCountQuery.data ?? loadedUnreadIds.length
  const panelRef = useRef<HTMLDivElement>(null)
  const buttonRef = useRef<HTMLButtonElement>(null)
  const listRef = useRef<HTMLUListElement>(null)
  const sentinelRef = useRef<HTMLLIElement>(null)

  useEffect(() => {
    if (!open) {
      return
    }

    function onPointerDown(event: PointerEvent) {
      if (!(event.target instanceof Node)) {
        return
      }

      if (panelRef.current?.contains(event.target) === true || buttonRef.current?.contains(event.target) === true) {
        return
      }

      setOpen(false)
    }

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setOpen(false)
        buttonRef.current?.focus()
      }
    }

    document.addEventListener('pointerdown', onPointerDown)
    document.addEventListener('keydown', onKeyDown)

    return () => {
      document.removeEventListener('pointerdown', onPointerDown)
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [open])

  const hasNextPage = notificationsQuery.hasNextPage === true
  const isFetchingNextPage = notificationsQuery.isFetchingNextPage
  const fetchNextPage = notificationsQuery.fetchNextPage

  // Jira-style incremental loading (F06 D3): the next page loads itself once
  // the sentinel row at the bottom of the scrollable list enters the list's
  // own viewport — an `IntersectionObserver` scoped to that one scroll
  // container (`root`), never a `scroll` event listener polled on every
  // frame. Never fires while a page is already in flight or once the server
  // says there is no next page (`hasNextPage`).
  const isFetchNextPageError = notificationsQuery.isFetchNextPageError

  useEffect(() => {
    if (!open || !hasNextPage || isFetchNextPageError) {
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
  }, [open, hasNextPage, isFetchingNextPage, isFetchNextPageError, fetchNextPage])

  return (
    <div className="relative">
      <button
        type="button"
        ref={buttonRef}
        className="relative inline-flex size-10 cursor-pointer items-center justify-center rounded-pill border border-border bg-surface text-text hover:bg-column"
        aria-haspopup="true"
        aria-expanded={open}
        aria-label={unreadCount > 0 ? `Notifications, ${unreadCount} unread` : 'Notifications'}
        onClick={() => setOpen((value) => !value)}
      >
        <BellIcon />
        {unreadCount > 0 && (
          <span className="absolute -top-1 -right-1 inline-flex h-4 min-w-[16px] items-center justify-center rounded-pill bg-danger px-[3px] text-[10px] font-bold text-text-inverse">
            {unreadCount}
          </span>
        )}
      </button>

      {open && (
        <div
          ref={panelRef}
          role="menu"
          aria-label="Notifications"
          className="absolute top-[calc(100%+var(--space-2))] right-0 z-50 flex w-[320px] max-w-[90vw] flex-col gap-[var(--space-2)] rounded-xl bg-surface-raised p-[var(--space-2)] shadow-raised"
        >
          <div className="flex items-center justify-between gap-[var(--space-2)] px-[var(--space-1)]">
            <span className="text-[length:var(--font-size-sm)] font-semibold text-text">Notifications</span>
            <button
              type="button"
              className="cursor-pointer border-0 bg-transparent p-0 text-[length:var(--font-size-xs)] font-medium text-accent-ink underline disabled:cursor-not-allowed disabled:text-text-muted disabled:no-underline"
              disabled={loadedUnreadIds.length === 0 || markAllReadMutation.isPending}
              onClick={() => markAllReadMutation.mutate(items[0]?.id)}
            >
              Mark all as read
            </button>
          </div>

          {notificationsQuery.isPending && (
            <p className="p-[var(--space-3)] text-[length:var(--font-size-sm)] text-text-muted">Loading…</p>
          )}
          {/*
           * A page-2+ fetch failure (`isFetchNextPageError`) must never hide
           * the rows already loaded — react-query reuses the overall query's
           * `isError`/`error` for that case too (its `status` reflects the
           * *last* fetch, not just the first), so this is scoped to a true
           * first-load failure (`items.length === 0`, nothing cached yet).
           * The page-2+ case gets its own inline Retry row below, inside the
           * list, instead.
           */}
          {notificationsQuery.isError && items.length === 0 && (
            <p className="p-[var(--space-3)] text-[length:var(--font-size-sm)] text-text-muted" role="alert">
              {describeError(notificationsQuery.error)}
            </p>
          )}
          {!notificationsQuery.isPending && !notificationsQuery.isError && items.length === 0 && (
            <p className="p-[var(--space-3)] text-[length:var(--font-size-sm)] text-text-muted">You&rsquo;re all caught up.</p>
          )}
          {items.length > 0 && (
            // `max-h-[320px]` ≈ 5 rows of this list's own markup (measured:
            // an unread row with its "Mark read" button is ~73.5px, a read
            // row without one is ~54px — "roughly 5" either way) — past that
            // the list scrolls inside itself instead of growing the popover
            // off-screen (layout fix, this task). `tabIndex={0}` makes the
            // scroll container itself keyboard-reachable (arrow/Page keys
            // scroll it once focused, same as a native scrollable element
            // anywhere else); the visible focus ring is the app-wide
            // `:focus-visible` rule (`global.css`), not a one-off here.
            // `scrollbar-gutter: stable` reserves the scrollbar's width
            // whether or not it is currently shown, so the few-notification
            // (no scroll) and many-notification (scroll) cases render at the
            // same width — no layout shift when paging introduces the first
            // scrollbar. The scrollbar's own look (thin, transparent track,
            // no arrow buttons) is the existing app-wide rule in
            // `global.css`, not new CSS.
            <ul
              ref={listRef}
              tabIndex={0}
              aria-label="Notification list"
              className="m-0 flex max-h-[320px] list-none flex-col gap-[2px] overflow-y-auto p-0 [scrollbar-gutter:stable]"
            >
              {items.map((notification) => (
                <li
                  key={notification.id}
                  className="flex items-start gap-[var(--space-2)] rounded-md px-[var(--space-3)] py-[var(--space-2)] hover:bg-column"
                >
                  <span className="relative shrink-0">
                    {notification.actor !== null ? (
                      <Avatar
                        userId={notification.actor.id}
                        name={usersQuery.data?.find((user) => user.id === notification.actor!.id)?.displayName ?? notification.actor.userName}
                        avatarUrl={usersQuery.data?.find((user) => user.id === notification.actor!.id)?.avatarUrl ?? null}
                        size={24}
                      />
                    ) : (
                      <span className="flex size-6 items-center justify-center rounded-pill bg-column text-[9px] font-semibold text-text-muted" aria-hidden="true">
                        ?
                      </span>
                    )}
                    {!notification.isRead && (
                      <span
                        aria-hidden="true"
                        className="absolute -top-px -right-px size-2 rounded-pill bg-accent"
                        style={{ boxShadow: '0 0 0 2px var(--color-surface-raised)' }}
                      />
                    )}
                  </span>
                  {/*
                   * Title and time stacked, not side by side (feedback this
                   * task): inline, a long title wrapping to a second line
                   * pushed the time out of line with every single-line row's
                   * time. Stacked, the time's position is the same relative
                   * to its own title regardless of how many lines that title
                   * takes.
                   */}
                  <div className="flex min-w-0 flex-1 flex-col gap-[2px]">
                    <Link
                      to={`/tasks/${notification.taskId}`}
                      className={clsx(
                        'text-[length:var(--font-size-sm)] no-underline hover:underline',
                        notification.isRead ? 'font-normal text-text-muted' : 'font-semibold text-text',
                      )}
                      onClick={() => {
                        // Mouse click and keyboard Enter on a focused `<a>`
                        // both fire this same `click` event — no separate
                        // keydown handler needed. Fires the mark-read request
                        // (cache updates immediately, see the mutation's own
                        // doc comment) and lets navigation proceed right
                        // after; never re-sent for an already-read row.
                        if (!notification.isRead) {
                          markReadMutation.mutate(notification.id)
                        }

                        setOpen(false)
                      }}
                    >
                      &ldquo;{notification.task.title}&rdquo; was sent back for rework
                      {notification.actor !== null &&
                        ` by ${usersQuery.data?.find((user) => user.id === notification.actor!.id)?.displayName ?? notification.actor.userName}`}
                    </Link>
                    <time dateTime={notification.createdAt} className="text-[length:var(--font-size-2xs)] text-text-muted">
                      {formatRelativeTime(notification.createdAt)}
                    </time>
                  </div>
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

              {/*
               * The `IntersectionObserver` target: invisible, no text —
               * entering the list's own scroll viewport is what triggers
               * `fetchNextPage`, never a label a screen reader should read.
               * Rendered only while there is a next page to fetch and no
               * fetch is already failed (a failed fetch instead shows the
               * Retry button below, which must stay keyboard/AT reachable).
               */}
              {hasNextPage && !isFetchNextPageError && <li ref={sentinelRef} aria-hidden="true" className="h-px" />}

              {(isFetchingNextPage || isFetchNextPageError || (!hasNextPage && (lastPagination?.totalPages ?? 1) > 1)) && (
                <li aria-live="polite" className="flex justify-center p-[var(--space-2)] text-[length:var(--font-size-xs)] text-text-muted">
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
                    'No more notifications'
                  )}
                </li>
              )}
            </ul>
          )}
        </div>
      )}

      {toastMessage !== null && <MoveToast message={toastMessage} onDismiss={() => setToastMessage(null)} />}
    </div>
  )
}
