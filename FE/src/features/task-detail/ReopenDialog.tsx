import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'

import styles from './ReopenDialog.module.css'

interface ReopenDialogProps {
  taskTitle: string
  isPending: boolean
  onCancel: () => void
  onSubmit: (reason: string) => void
}

const MAX_REASON_LENGTH = 500

/**
 * `Done → Review` reopen (F01 D6), Manager/Admin only (the parent only renders
 * this when `allowedTransitions` carries the `Reopen` edge, so the caller
 * already has `ReopenTasks` by the time it opens). A reason is required
 * (AC22); the input survives a failed submit so the user does not retype it.
 */
export function ReopenDialog({ taskTitle, isPending, onCancel, onSubmit }: ReopenDialogProps) {
  const [reason, setReason] = useState('')
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const previouslyFocused = useRef<Element | null>(null)

  // Latest callback/flag read from the keydown handler below, so the mount
  // effect that sets up focus management can run exactly once.
  const onCancelRef = useRef(onCancel)
  const isPendingRef = useRef(isPending)

  useEffect(() => {
    onCancelRef.current = onCancel
    isPendingRef.current = isPending
  })

  useEffect(() => {
    previouslyFocused.current = document.activeElement
    textareaRef.current?.focus()

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape' && !isPendingRef.current) {
        onCancelRef.current()
      }
    }

    document.addEventListener('keydown', onKeyDown)

    return () => {
      document.removeEventListener('keydown', onKeyDown)
      if (previouslyFocused.current instanceof HTMLElement) {
        previouslyFocused.current.focus()
      }
    }
  }, [])

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    const trimmed = reason.trim()

    if (trimmed.length === 0) {
      return
    }

    onSubmit(trimmed)
  }

  return (
    <div className={styles.overlay} onClick={() => !isPending && onCancel()}>
      <form
        role="dialog"
        aria-modal="true"
        aria-labelledby="reopen-title"
        className={styles.dialog}
        onClick={(event) => event.stopPropagation()}
        onSubmit={handleSubmit}
      >
        <h2 id="reopen-title" className={styles.title}>
          Reopen &ldquo;{taskTitle}&rdquo;
        </h2>
        <p className={styles.hint}>Say why it needs more work. This moves the task back to Review.</p>
        <label className={styles.label} htmlFor="reopen-reason">
          Reason
        </label>
        <textarea
          id="reopen-reason"
          ref={textareaRef}
          className={styles.textarea}
          value={reason}
          maxLength={MAX_REASON_LENGTH}
          required
          disabled={isPending}
          onChange={(event) => setReason(event.target.value)}
        />
        <div className={styles.actions}>
          <button type="submit" className={styles.primaryAction} disabled={isPending || reason.trim().length === 0}>
            {isPending ? 'Reopening…' : 'Reopen'}
          </button>
          <button type="button" className={styles.secondaryAction} onClick={onCancel} disabled={isPending}>
            Cancel
          </button>
        </div>
      </form>
    </div>
  )
}
