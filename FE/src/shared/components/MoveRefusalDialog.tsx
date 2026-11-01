import { useEffect, useRef } from 'react'

import { describeErrorCode } from '../../api/errorMessages'
import { DEFAULT_REFUSAL_TITLE, MOVE_REFUSAL_TITLES } from '../lib/moveRefusalTitles'

import styles from './MoveRefusalDialog.module.css'

interface MoveRefusalDialogProps {
  code: string | null
  taskTitle: string
  onClose: () => void
}

/**
 * The blocked-move dialog (`docs/ui/screens/BlockedActions.dc.html`): names
 * the task and the rule, then offers the one way out — here, just
 * acknowledging it and trying a legal move instead, since the dialog closes
 * onto a board/detail view that already shows the legal moves as buttons.
 * `role="alertdialog"` moves focus in on open and returns it on close
 * (`frontend.md` §8).
 */
export function MoveRefusalDialog({ code, taskTitle, onClose }: MoveRefusalDialogProps) {
  const closeButtonRef = useRef<HTMLButtonElement>(null)
  const previouslyFocused = useRef<Element | null>(null)

  useEffect(() => {
    previouslyFocused.current = document.activeElement
    closeButtonRef.current?.focus()

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        onClose()
      }
    }

    document.addEventListener('keydown', onKeyDown)

    return () => {
      document.removeEventListener('keydown', onKeyDown)
      if (previouslyFocused.current instanceof HTMLElement) {
        previouslyFocused.current.focus()
      }
    }
  }, [onClose])

  const title = (code !== null && MOVE_REFUSAL_TITLES[code]) || DEFAULT_REFUSAL_TITLE

  return (
    <div className={styles.overlay} onClick={onClose}>
      <div
        role="alertdialog"
        aria-modal="true"
        aria-labelledby="move-refusal-title"
        aria-describedby="move-refusal-description"
        className={styles.dialog}
        onClick={(event) => event.stopPropagation()}
      >
        <h2 id="move-refusal-title" className={styles.title}>
          {title}
        </h2>
        <p id="move-refusal-description" className={styles.description}>
          &ldquo;{taskTitle}&rdquo; — {describeErrorCode(code)}
        </p>
        <button type="button" ref={closeButtonRef} className={styles.closeButton} onClick={onClose}>
          Close
        </button>
      </div>
    </div>
  )
}
