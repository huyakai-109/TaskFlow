import clsx from 'clsx'
import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'

import { describeError } from '../../../api/errorMessages'
import { useMarkAllNotificationsReadMutation } from '../hooks/useMarkAllNotificationsReadMutation'
import { useMarkNotificationReadMutation } from '../hooks/useMarkNotificationReadMutation'
import { useNotificationsQuery } from '../hooks/useNotificationsQuery'

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
 * Shows both read and unread rows (`useNotificationsQuery`, fixed this task
 * — a read notification used to disappear from the list entirely, a
 * client-side filter bug, not an API limitation; see that hook's own doc
 * comment). The badge counts unread only, never the total.
 */
export function NotificationBell() {
  const [open, setOpen] = useState(false)
  const notificationsQuery = useNotificationsQuery()
  const markReadMutation = useMarkNotificationReadMutation()
  const markAllReadMutation = useMarkAllNotificationsReadMutation()
  const items = notificationsQuery.data?.result ?? []
  const unreadIds = items.filter((notification) => !notification.isRead).map((notification) => notification.id)
  const unreadCount = unreadIds.length
  const panelRef = useRef<HTMLDivElement>(null)
  const buttonRef = useRef<HTMLButtonElement>(null)

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
              disabled={unreadCount === 0 || markAllReadMutation.isPending}
              onClick={() => markAllReadMutation.mutate(unreadIds)}
            >
              Mark all as read
            </button>
          </div>

          {notificationsQuery.isPending && (
            <p className="p-[var(--space-3)] text-[length:var(--font-size-sm)] text-text-muted">Loading…</p>
          )}
          {notificationsQuery.isError && (
            <p className="p-[var(--space-3)] text-[length:var(--font-size-sm)] text-text-muted" role="alert">
              {describeError(notificationsQuery.error)}
            </p>
          )}
          {notificationsQuery.isSuccess && items.length === 0 && (
            <p className="p-[var(--space-3)] text-[length:var(--font-size-sm)] text-text-muted">You&rsquo;re all caught up.</p>
          )}
          {notificationsQuery.isSuccess && items.length > 0 && (
            <ul className="m-0 flex max-h-[360px] list-none flex-col gap-[2px] overflow-y-auto p-0">
              {items.map((notification) => (
                <li
                  key={notification.id}
                  className="flex items-start gap-[var(--space-2)] rounded-md px-[var(--space-3)] py-[var(--space-2)] hover:bg-column"
                >
                  <span
                    aria-hidden="true"
                    className={clsx(
                      'mt-[6px] size-2 shrink-0 rounded-pill',
                      notification.isRead ? 'bg-transparent' : 'bg-accent',
                    )}
                  />
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
                      onClick={() => setOpen(false)}
                    >
                      Task #{notification.taskId} was sent back for rework
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
            </ul>
          )}
        </div>
      )}
    </div>
  )
}
