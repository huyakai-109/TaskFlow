import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'

import { describeError } from '../../api/errorMessages'

import styles from './NotificationBell.module.css'
import { useMarkNotificationReadMutation } from './useMarkNotificationReadMutation'
import { useNotificationsQuery } from './useNotificationsQuery'

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
 * draws this (`docs/ui.md` "Not designed yet" lists "notification inbox"), so
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
    <div className={styles.wrapper}>
      <button
        type="button"
        ref={buttonRef}
        className={styles.bellButton}
        aria-haspopup="true"
        aria-expanded={open}
        aria-label={items.length > 0 ? `Notifications, ${items.length} unread` : 'Notifications'}
        onClick={() => setOpen((value) => !value)}
      >
        <BellIcon />
        {items.length > 0 && <span className={styles.badge}>{items.length}</span>}
      </button>

      {open && (
        <div ref={panelRef} role="menu" aria-label="Notifications" className={styles.panel}>
          {notificationsQuery.isPending && <p className={styles.note}>Loading…</p>}
          {notificationsQuery.isError && (
            <p className={styles.note} role="alert">
              {describeError(notificationsQuery.error)}
            </p>
          )}
          {notificationsQuery.isSuccess && items.length === 0 && <p className={styles.note}>You&rsquo;re all caught up.</p>}
          {notificationsQuery.isSuccess && items.length > 0 && (
            <ul className={styles.list}>
              {items.map((notification) => (
                <li key={notification.id} className={styles.item}>
                  <Link to={`/tasks/${notification.taskId}`} className={styles.itemLink} onClick={() => setOpen(false)}>
                    Task #{notification.taskId} was sent back for rework
                  </Link>
                  <button
                    type="button"
                    className={styles.markRead}
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
