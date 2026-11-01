import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'

const actionButtonClass =
  'min-h-11 cursor-pointer rounded-md px-[var(--space-4)] disabled:cursor-not-allowed disabled:opacity-60'

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
    <div
      className="fixed inset-0 z-[100] flex items-center justify-center bg-[rgb(16_16_24_/_40%)] p-[var(--space-4)]"
      onClick={() => !isPending && onCancel()}
    >
      <form
        role="dialog"
        aria-modal="true"
        aria-labelledby="reopen-title"
        className="flex w-full max-w-[440px] flex-col gap-[var(--space-3)] rounded-xl bg-surface-raised p-[var(--space-5)] shadow-raised"
        onClick={(event) => event.stopPropagation()}
        onSubmit={handleSubmit}
      >
        <h2 id="reopen-title" className="text-[length:var(--font-size-lg)] font-semibold">
          Reopen &ldquo;{taskTitle}&rdquo;
        </h2>
        <p className="text-[length:var(--font-size-sm)] text-text-muted">
          Say why it needs more work. This moves the task back to Review.
        </p>
        <label className="text-[length:var(--font-size-sm)] font-medium" htmlFor="reopen-reason">
          Reason
        </label>
        <textarea
          id="reopen-reason"
          ref={textareaRef}
          className="min-h-[96px] resize-y rounded-md border border-border bg-surface p-[var(--space-3)] text-text [font:inherit]"
          value={reason}
          maxLength={MAX_REASON_LENGTH}
          required
          disabled={isPending}
          onChange={(event) => setReason(event.target.value)}
        />
        <div className="flex gap-[var(--space-3)]">
          <button
            type="submit"
            className={`${actionButtonClass} border-0 bg-accent font-semibold text-on-accent [&:hover:not(:disabled)]:bg-accent-hover`}
            disabled={isPending || reason.trim().length === 0}
          >
            {isPending ? 'Reopening…' : 'Reopen'}
          </button>
          <button
            type="button"
            className={`${actionButtonClass} border border-border bg-surface font-medium text-text`}
            onClick={onCancel}
            disabled={isPending}
          >
            Cancel
          </button>
        </div>
      </form>
    </div>
  )
}
