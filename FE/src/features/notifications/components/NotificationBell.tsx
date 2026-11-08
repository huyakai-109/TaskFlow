import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'

import { describeError } from '../../../api/errorMessages'
import { useMarkNotificationReadMutation } from '../hooks/useMarkNotificationReadMutation'
import { useNotificationsQuery } from '../hooks/useNotificationsQuery'

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
 */
export function NotificationBell() {
  const [open, setOpen] = useState(false)
  const notificationsQuery = useNotificationsQuery()
  const markReadMutation = useMarkNotificationReadMutation()
  const items = notificationsQuery.data?.result ?? []
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
        aria-label={items.length > 0 ? `Notifications, ${items.length} unread` : 'Notifications'}
        onClick={() => setOpen((value) => !value)}
      >
        <BellIcon />
        {items.length > 0 && (
          <span className="absolute -top-1 -right-1 inline-flex h-4 min-w-[16px] items-center justify-center rounded-pill bg-danger px-[3px] text-[10px] font-bold text-text-inverse">
            {items.length}
          </span>
        )}
      </button>

      {open && (
        <div
          ref={panelRef}
          role="menu"
          aria-label="Notifications"
          className="absolute top-[calc(100%+var(--space-2))] right-0 z-50 w-[320px] max-w-[90vw] rounded-xl bg-surface-raised p-[var(--space-2)] shadow-raised"
        >
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
            <ul className="m-0 flex list-none flex-col gap-[2px] p-0">
              {items.map((notification) => (
                <li
                  key={notification.id}
                  className="flex items-center gap-[var(--space-2)] rounded-md px-[var(--space-3)] py-[var(--space-2)] hover:bg-column"
                >
                  <Link
                    to={`/tasks/${notification.taskId}`}
                    className="grow text-[length:var(--font-size-sm)] text-text no-underline hover:underline"
                    onClick={() => setOpen(false)}
                  >
                    Task #{notification.taskId} was sent back for rework
                  </Link>
                  <button
                    type="button"
                    className="shrink-0 cursor-pointer rounded-sm border border-border bg-surface px-[var(--space-2)] py-[4px] text-[length:var(--font-size-xs)] text-text-muted hover:bg-accent-soft hover:text-accent-ink"
                    onClick={() => markReadMutation.mutate(notification.id)}
                  >
                    Mark read
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  )
}
