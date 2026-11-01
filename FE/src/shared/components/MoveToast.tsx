import { useEffect } from 'react'

import styles from './MoveToast.module.css'

interface MoveToastProps {
  message: string
  onDismiss: () => void
}

/**
 * The dark pill toast `docs/ui.md` calls for ("a short confirmation"), reused
 * here for a conflict/not-found move: the board or task view has already
 * refetched by the time this shows, so it is a notice, not a blocking dialog.
 */
export function MoveToast({ message, onDismiss }: MoveToastProps) {
  useEffect(() => {
    const timer = window.setTimeout(onDismiss, 6000)
    return () => window.clearTimeout(timer)
  }, [onDismiss])

  return (
    <div className={styles.toast} role="alert">
      <span>{message}</span>
      <button type="button" className={styles.dismiss} onClick={onDismiss} aria-label="Dismiss">
        <svg aria-hidden="true" width="12" height="12" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round">
          <path d="M3 3l10 10M13 3 3 13" />
        </svg>
      </button>
    </div>
  )
}
